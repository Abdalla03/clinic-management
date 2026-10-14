using ClinicManagement.Domain.Patients;
using ClinicManagement.Domain.Users;
using ClinicManagement.Infrastructure.Persistence;
using ClinicManagement.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace ClinicManagement.IntegrationTests;

/// <summary>
/// Proves the <c>[EncryptedColumn]</c> converter is actually wired in the running host: the row on
/// disk must not contain the plaintext, while EF must still hand the plaintext back to the caller.
/// </summary>
[Collection(ClinicApiCollection.Name)]
public sealed class FieldEncryptionTests
{
    private const string Diagnosis = "Essential hypertension, stage 1";
    private const string VisitNotes = "Advised low sodium diet and daily blood pressure monitoring.";
    private const string NationalId = "28501120100573";

    private readonly ClinicApiFactory _factory;

    public FieldEncryptionTests(ClinicApiFactory factory)
    {
        _factory = factory;
    }

    [RequiresDockerFact]
    public async Task DiagnosisAndNotes_AreStoredAsCiphertextButReadBackAsPlaintext()
    {
        const string email = "encrypt-doctor@clinic.test";
        await _factory.CreateUserAsync(email, TestApi.Password, RoleNames.Doctor);

        Guid visitId;
        Guid patientId;
        Guid doctorId;

        using (var scope = _factory.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ClinicDbContext>();
            var user = await context.Users.SingleAsync(candidate => candidate.Email == email);

            var doctor = Doctor.Create(user.Id, "Cardiology", "LIC-2026-ENC", DateTime.UtcNow);
            var patient = Patient.Create("MRN-2026-ENC", "Mona", "Adel", DateTime.UtcNow);
            patient.UpdateContactDetails(NationalId, "+201001112233", "Karim Adel", "+201223334455", DateTime.UtcNow);

            context.Doctors.Add(doctor);
            context.Patients.Add(patient);
            await context.SaveChangesAsync();

            var visit = PatientVisit.Record(
                patient.Id,
                doctor.Id,
                appointmentId: null,
                DateTime.UtcNow,
                chiefComplaint: "Headache for three days",
                notes: VisitNotes,
                diagnosis: Diagnosis,
                recordedByUserId: user.Id,
                nowUtc: DateTime.UtcNow);

            context.PatientVisits.Add(visit);
            await context.SaveChangesAsync();

            visitId = visit.Id;
            patientId = patient.Id;
            doctorId = doctor.Id;
        }

        try
        {
            var storedDiagnosis = await ReadRawAsync("\"PatientVisits\"", "\"Diagnosis\"", visitId);
            var storedNotes = await ReadRawAsync("\"PatientVisits\"", "\"Notes\"", visitId);
            var storedNationalId = await ReadRawAsync("\"Patients\"", "\"NationalId\"", patientId);

            Assert.NotNull(storedDiagnosis);
            Assert.DoesNotContain(Diagnosis, storedDiagnosis, StringComparison.Ordinal);
            Assert.DoesNotContain(VisitNotes, storedNotes!, StringComparison.Ordinal);
            Assert.DoesNotContain(NationalId, storedNationalId!, StringComparison.Ordinal);

            using (var scope = _factory.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<ClinicDbContext>();
                var visit = await context.PatientVisits.SingleAsync(candidate => candidate.Id == visitId);

                Assert.Equal(Diagnosis, visit.Diagnosis);
                Assert.Equal(VisitNotes, visit.Notes);

                var patient = await context.Patients.SingleAsync(candidate => candidate.Id == patientId);
                Assert.Equal(NationalId, patient.NationalId);
            }
        }
        finally
        {
            await CleanupAsync(email, doctorId, patientId, visitId);
        }
    }

    private async Task<string?> ReadRawAsync(string table, string column, Guid id)
    {
        using var scope = _factory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ClinicDbContext>();
        var connectionString = context.Database.GetConnectionString()
            ?? throw new InvalidOperationException("The test host has no connection string configured.");

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand($"SELECT {column} FROM {table} WHERE \"Id\" = @id", connection);
        command.Parameters.AddWithValue("id", id);

        return (string?)await command.ExecuteScalarAsync();
    }

    private async Task CleanupAsync(string email, Guid doctorId, Guid patientId, Guid visitId)
    {
        using (var scope = _factory.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ClinicDbContext>();

            var visits = context.PatientVisits.Where(candidate => candidate.Id == visitId);
            context.PatientVisits.RemoveRange(visits);

            var prescriptions = context.Prescriptions.Where(candidate => candidate.PatientId == patientId);
            context.Prescriptions.RemoveRange(prescriptions);

            var appointments = context.Appointments
                .Where(candidate => candidate.DoctorId == doctorId || candidate.PatientId == patientId);
            context.Appointments.RemoveRange(appointments);

            var doctor = await context.Doctors.FindAsync(doctorId);
            if (doctor is not null)
            {
                context.Doctors.Remove(doctor);
            }

            var patient = await context.Patients.FindAsync(patientId);
            if (patient is not null)
            {
                context.Patients.Remove(patient);
            }

            await context.SaveChangesAsync();
        }

        await _factory.DeleteUserAsync(email);
    }
}
