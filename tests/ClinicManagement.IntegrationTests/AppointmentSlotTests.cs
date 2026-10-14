using ClinicManagement.Domain.Appointments;
using ClinicManagement.Domain.Patients;
using ClinicManagement.Domain.Users;
using ClinicManagement.Infrastructure.Persistence;
using ClinicManagement.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace ClinicManagement.IntegrationTests;

[Collection(ClinicApiCollection.Name)]
public sealed class AppointmentSlotTests
{
    private static readonly DateTime SlotStart = new(2026, 5, 4, 9, 0, 0, DateTimeKind.Utc);

    private readonly ClinicApiFactory _factory;

    public AppointmentSlotTests(ClinicApiFactory factory)
    {
        _factory = factory;
    }

    [RequiresDockerFact]
    public async Task TwoActiveAppointmentsForTheSameDoctorAndStart_AreRejectedByTheUniqueIndex()
    {
        const string email = "slot-doctor@clinic.test";
        await _factory.CreateUserAsync(email, TestApi.Password, RoleNames.Doctor);

        Guid doctorId;
        Guid patientId;

        using (var scope = _factory.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ClinicDbContext>();
            var user = await context.Users.SingleAsync(candidate => candidate.Email == email);

            var doctor = Doctor.Create(user.Id, "Internal medicine", "LIC-2026-0001", DateTime.UtcNow);
            var patient = Patient.Create("MRN-2026-0001", "Sara", "Mahmoud", DateTime.UtcNow);

            context.Doctors.Add(doctor);
            context.Patients.Add(patient);
            await context.SaveChangesAsync();

            doctorId = doctor.Id;
            patientId = patient.Id;
        }

        try
        {
            using (var scope = _factory.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<ClinicDbContext>();

                context.Appointments.Add(Appointment.Book(
                    patientId, doctorId, SlotStart, SlotStart.AddMinutes(30), null, doctorId, DateTime.UtcNow));
                await context.SaveChangesAsync();

                context.Appointments.Add(Appointment.Book(
                    patientId, doctorId, SlotStart, SlotStart.AddMinutes(30), null, doctorId, DateTime.UtcNow));

                var exception = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
                var postgres = Assert.IsType<PostgresException>(exception.InnerException);

                Assert.Equal(PostgresErrorCodes.UniqueViolation, postgres.SqlState);
                Assert.Contains("UX_Appointments_Doctor_SlotKey", postgres.ConstraintName, StringComparison.Ordinal);
            }

            using (var scope = _factory.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<ClinicDbContext>();
                var appointment = await context.Appointments.SingleAsync(candidate => candidate.DoctorId == doctorId);

                appointment.Cancel("freed by test", DateTime.UtcNow);
                await context.SaveChangesAsync();

                context.Appointments.Add(Appointment.Book(
                    patientId, doctorId, SlotStart, SlotStart.AddMinutes(30), null, doctorId, DateTime.UtcNow));
                await context.SaveChangesAsync();

                Assert.Equal(2, await context.Appointments.CountAsync(candidate => candidate.DoctorId == doctorId));
            }
        }
        finally
        {
            await CleanupAsync(email, doctorId, patientId);
        }
    }

    private async Task CleanupAsync(string email, Guid doctorId, Guid patientId)
    {
        using (var scope = _factory.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ClinicDbContext>();
            var appointments = context.Appointments.Where(candidate => candidate.DoctorId == doctorId || candidate.PatientId == patientId);
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