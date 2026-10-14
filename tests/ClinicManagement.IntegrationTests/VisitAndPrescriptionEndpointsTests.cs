using System.Net;
using System.Net.Http.Json;
using ClinicManagement.Application.Common.Paging;
using ClinicManagement.Application.Prescriptions;
using ClinicManagement.Application.Visits;
using ClinicManagement.Domain.Appointments;
using ClinicManagement.Domain.Auditing;
using ClinicManagement.Domain.Common;
using ClinicManagement.Domain.Patients;
using ClinicManagement.Domain.Prescriptions;
using ClinicManagement.Domain.Users;
using ClinicManagement.Infrastructure.Persistence;
using ClinicManagement.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace ClinicManagement.IntegrationTests;

[Collection(ClinicApiCollection.Name)]
public sealed class VisitAndPrescriptionEndpointsTests : IDisposable
{
    private const string Diagnosis = "Essential hypertension, stage 1";
    private const string VisitNotes = "Advised low sodium diet and daily blood pressure monitoring.";
    private const string DrugName = "Amlodipine";

    private readonly ClinicApiFactory _factory;
    private readonly string _reference = $"VRX-{Guid.NewGuid().ToString("N")[..8]}";
    private readonly List<string> _emails = [];
    private readonly List<Guid> _doctorIds = [];
    private readonly List<Guid> _patientIds = [];

    private HttpClient _doctor = null!;
    private HttpClient _secretary = null!;
    private HttpClient _patient = null!;
    private Guid _doctorId;
    private Guid _doctorUserId;
    private Guid _patientId;
    private Guid _otherPatientId;
    private string? _doctorEmail;

    public VisitAndPrescriptionEndpointsTests(ClinicApiFactory factory)
    {
        _factory = factory;
    }

    [RequiresDockerFact]
    public async Task AnonymousCallerCannotRecordAVisit()
    {
        await ArrangeAsync();

        var response = await _factory.CreateClient().PostAsJsonAsync(
            "/api/visits",
            new RecordVisitRequest(_patientId, _doctorId, DateTime.UtcNow));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [RequiresDockerFact]
    public async Task SecretaryCannotRecordAVisitButCanReadOneTheDoctorRecorded()
    {
        await ArrangeAsync();

        var denied = await _secretary.PostAsJsonAsync(
            "/api/visits",
            new RecordVisitRequest(_patientId, _doctorId, DateTime.UtcNow, Diagnosis: Diagnosis));
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);

        var visit = await RecordVisitAsync();

        var fetched = await _secretary.GetAsync($"/api/visits/{visit.Id}");
        Assert.Equal(HttpStatusCode.OK, fetched.StatusCode);

        var details = await fetched.Content.ReadFromJsonAsync<VisitDetails>();
        Assert.Equal(Diagnosis, details!.Diagnosis);
    }

    [RequiresDockerFact]
    public async Task DoctorRecordsAVisitAndThePatientReadsTheirOwnCopy()
    {
        await ArrangeAsync();

        var visit = await RecordVisitAsync();

        Assert.Equal(_patientId, visit.PatientId);
        Assert.Equal(_doctorId, visit.DoctorId);
        Assert.Equal(Diagnosis, visit.Diagnosis);
        Assert.Equal(VisitNotes, visit.Notes);
        Assert.False(string.IsNullOrWhiteSpace(visit.PatientName));
        Assert.False(string.IsNullOrWhiteSpace(visit.DoctorName));

        var own = await _patient.GetAsync("/api/visits/mine");
        Assert.Equal(HttpStatusCode.OK, own.StatusCode);

        var page = await own.Content.ReadFromJsonAsync<PagedResult<VisitSummary>>();
        Assert.Contains(page!.Items, item => item.Id == visit.Id);
        Assert.Contains(page.Items, item => item.Diagnosis == Diagnosis);

        (await _patient.GetAsync($"/api/visits/{visit.Id}")).EnsureSuccessStatusCode();

        using var scope = _factory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ClinicDbContext>();
        var entries = await context.AuditLogEntries
            .Where(entry => entry.PatientId == _patientId && entry.EntityName == nameof(PatientVisit))
            .ToListAsync();

        Assert.Contains(entries, entry => entry.Action == AuditAction.Created && entry.EntityId == visit.Id.ToString());
        Assert.Contains(entries, entry => entry.Action == AuditAction.Viewed);
    }

    [RequiresDockerFact]
    public async Task AmendingAVisitRecordsTheChangedClinicalFields()
    {
        await ArrangeAsync();

        var visit = await RecordVisitAsync();

        var update = await _doctor.PutAsJsonAsync(
            $"/api/visits/{visit.Id}",
            new UpdateVisitRequest(
                ChiefComplaint: "Headache, now resolved",
                Notes: VisitNotes,
                Diagnosis: "Hypertension, controlled on medication"));

        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        var amended = await update.Content.ReadFromJsonAsync<VisitDetails>();
        Assert.Equal("Hypertension, controlled on medication", amended!.Diagnosis);
        Assert.Equal("Headache, now resolved", amended.ChiefComplaint);
        Assert.Equal(VisitNotes, amended.Notes);

        using var scope = _factory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ClinicDbContext>();
        var entry = await context.AuditLogEntries
            .Where(candidate => candidate.PatientId == _patientId &&
                                candidate.EntityName == nameof(PatientVisit) &&
                                candidate.Action == AuditAction.Updated)
            .OrderByDescending(candidate => candidate.Id)
            .FirstAsync();

        Assert.Equal(visit.Id.ToString(), entry.EntityId);
        Assert.Contains(nameof(PatientVisit.Diagnosis), entry.ChangedFields!.Split(','));
        Assert.Contains(nameof(PatientVisit.ChiefComplaint), entry.ChangedFields.Split(','));
    }

    [RequiresDockerFact]
    public async Task PatientCannotReadAnotherPatientsVisitNorRecordOne()
    {
        await ArrangeAsync();

        var visit = await RecordVisitAsync();

        var record = await _patient.PostAsJsonAsync(
            "/api/visits",
            new RecordVisitRequest(_patientId, _doctorId, DateTime.UtcNow));
        Assert.Equal(HttpStatusCode.Forbidden, record.StatusCode);

        var amend = await _patient.PutAsJsonAsync(
            $"/api/visits/{visit.Id}",
            new UpdateVisitRequest(Diagnosis: "Hacked"));
        Assert.Equal(HttpStatusCode.Forbidden, amend.StatusCode);

        var list = await _patient.GetAsync($"/api/visits?patientId={_otherPatientId}");
        Assert.Equal(HttpStatusCode.Forbidden, list.StatusCode);

        var foreign = await _patient.PostAsJsonAsync(
            "/api/visits",
            new RecordVisitRequest(_otherPatientId, _doctorId, DateTime.UtcNow));
        Assert.Equal(HttpStatusCode.Forbidden, foreign.StatusCode);
    }

    [RequiresDockerFact]
    public async Task AVisitMustBelongToABookableAppointment()
    {
        await ArrangeAsync();

        var foreignAppointment = await CreateAppointmentAsync(_otherPatientId, cancelled: false);
        var wrongPatient = await _doctor.PostAsJsonAsync(
            "/api/visits",
            new RecordVisitRequest(_patientId, _doctorId, DateTime.UtcNow, AppointmentId: foreignAppointment));
        Assert.Equal(HttpStatusCode.BadRequest, wrongPatient.StatusCode);

        var cancelledAppointment = await CreateAppointmentAsync(_patientId, cancelled: true);
        var cancelled = await _doctor.PostAsJsonAsync(
            "/api/visits",
            new RecordVisitRequest(_patientId, _doctorId, DateTime.UtcNow, AppointmentId: cancelledAppointment));
        Assert.Equal(HttpStatusCode.Conflict, cancelled.StatusCode);
        Assert.Equal(ErrorCodes.InvalidStateTransition, await ProblemCodeAsync(cancelled));
    }

    [RequiresDockerFact]
    public async Task DoctorIssuesAPrescriptionThatThePatientCanRead()
    {
        await ArrangeAsync();

        var visit = await RecordVisitAsync();

        var issue = await _doctor.PostAsJsonAsync(
            "/api/prescriptions",
            new IssuePrescriptionRequest(
                _patientId,
                Items:
                [
                    new PrescriptionItemRequest(
                        DrugName,
                        "5mg",
                        TimesPerDay: 1,
                        DurationDays: 30,
                        Route: "Oral",
                        Instructions: "Take in the morning.",
                        DoseTimes: [new TimeOnly(8, 0)])
                ],
                VisitId: visit.Id,
                Notes: "Monitor blood pressure at home."));

        var issueBody = await issue.Content.ReadAsStringAsync();
        Assert.True(issue.IsSuccessStatusCode, $"Issuing failed with {(int)issue.StatusCode}: {issueBody}");

        var prescription = (await issue.Content.ReadFromJsonAsync<PrescriptionDetails>())!;
        Assert.Equal(_patientId, prescription!.PatientId);
        Assert.Equal(_doctorId, prescription.DoctorId);
        Assert.Equal(visit.Id, prescription.VisitId);
        Assert.Equal("Monitor blood pressure at home.", prescription.Notes);

        var item = Assert.Single(prescription.Items);
        Assert.Equal(DrugName, item.DrugName);
        Assert.Equal("5mg", item.Dose);
        Assert.Equal(1, item.TimesPerDay);
        Assert.Equal(30, item.DurationDays);
        Assert.Equal(new TimeOnly(8, 0), Assert.Single(item.DoseTimes));
        Assert.Equal(AllergyCheckOutcome.NoKnownAllergy, item.AllergyCheckOutcome);

        var own = await _patient.GetAsync("/api/prescriptions/mine");
        Assert.Equal(HttpStatusCode.OK, own.StatusCode);

        var page = await own.Content.ReadFromJsonAsync<PagedResult<PrescriptionSummary>>();
        Assert.Contains(page!.Items, candidate => candidate.Id == prescription.Id && candidate.ItemCount == 1);
        Assert.Contains(page.Items, candidate => candidate.Id == prescription.Id && !candidate.ContainsAllergyMatch);

        var patientCopy = await _patient.GetAsync($"/api/prescriptions/{prescription.Id}");
        Assert.Equal(HttpStatusCode.OK, patientCopy.StatusCode);
    }

    [RequiresDockerFact]
    public async Task PrescribingASubstanceThePatientIsAllergicToIsFlagged()
    {
        await ArrangeAsync();

        Guid allergyId;
        using (var scope = _factory.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ClinicDbContext>();
            var patient = await context.Patients.SingleAsync(candidate => candidate.Id == _patientId);
            var doctorUser = await context.Users.SingleAsync(candidate => candidate.Id == _doctorUserId);

            var allergy = Allergy.Record(
                patient.Id,
                "Penicillin",
                reaction: "Hives",
                AllergySeverity.Severe,
                notes: "Confirmed by skin test",
                doctorUser.Id,
                DateTime.UtcNow);

            context.Allergies.Add(allergy);
            await context.SaveChangesAsync();
            allergyId = allergy.Id;
        }

        var issue = await _doctor.PostAsJsonAsync(
            "/api/prescriptions",
            new IssuePrescriptionRequest(
                _patientId,
                Items: [new PrescriptionItemRequest("penicillin", "500mg", TimesPerDay: 3, DurationDays: 7)]));

        Assert.Equal(HttpStatusCode.Created, issue.StatusCode);

        var prescription = await issue.Content.ReadFromJsonAsync<PrescriptionDetails>();
        var item = Assert.Single(prescription!.Items);

        Assert.Equal(AllergyCheckOutcome.MatchedAllergy, item.AllergyCheckOutcome);
        Assert.Equal(allergyId, item.MatchedAllergyId);

        var page = await (await _patient.GetAsync("/api/prescriptions/mine"))
            .Content.ReadFromJsonAsync<PagedResult<PrescriptionSummary>>();
        Assert.Contains(page!.Items, candidate => candidate.Id == prescription.Id && candidate.ContainsAllergyMatch);
    }

    [RequiresDockerFact]
    public async Task PrescriptionInputIsValidatedAndPatientsCannotIssue()
    {
        await ArrangeAsync();

        var invalid = await _doctor.PostAsJsonAsync(
            "/api/prescriptions",
            new IssuePrescriptionRequest(
                _patientId,
                Items: [new PrescriptionItemRequest(DrugName, "5mg", TimesPerDay: 0, DurationDays: 30)]));

        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal(ErrorCodes.ValidationFailed, await ProblemCodeAsync(invalid));

        var byPatient = await _patient.PostAsJsonAsync(
            "/api/prescriptions",
            new IssuePrescriptionRequest(
                _patientId,
                Items: [new PrescriptionItemRequest(DrugName, "5mg", TimesPerDay: 1, DurationDays: 30)]));

        Assert.Equal(HttpStatusCode.Forbidden, byPatient.StatusCode);

        var inactive = await _secretary.DeleteAsync($"/api/patients/{_patientId}");
        Assert.Equal(HttpStatusCode.NoContent, inactive.StatusCode);

        var forInactivePatient = await _doctor.PostAsJsonAsync(
            "/api/prescriptions",
            new IssuePrescriptionRequest(
                _patientId,
                Items: [new PrescriptionItemRequest(DrugName, "5mg", TimesPerDay: 1, DurationDays: 30)]));

        Assert.Equal(HttpStatusCode.Conflict, forInactivePatient.StatusCode);
    }

    [RequiresDockerFact]
    public async Task ClinicalTextOfVisitsAndPrescriptionsIsEncryptedAtRest()
    {
        await ArrangeAsync();

        var visit = await RecordVisitAsync();

        var issue = await _doctor.PostAsJsonAsync(
            "/api/prescriptions",
            new IssuePrescriptionRequest(
                _patientId,
                Items:
                [
                    new PrescriptionItemRequest(
                        DrugName,
                        "5mg",
                        TimesPerDay: 1,
                        DurationDays: 30,
                        Instructions: "Take with a full glass of water.")
                ],
                Notes: "Secret prescriber note"));

        Assert.Equal(HttpStatusCode.Created, issue.StatusCode);
        var prescription = await issue.Content.ReadFromJsonAsync<PrescriptionDetails>();

        var storedDiagnosis = await ReadRawAsync("\"PatientVisits\"", "\"Diagnosis\"", visit.Id);
        var storedNotes = await ReadRawAsync("\"Prescriptions\"", "\"Notes\"", prescription!.Id);
        var storedDose = await ReadRawAsync("\"PrescriptionItems\"", "\"Dose\"", prescription.Items[0].Id);

        Assert.DoesNotContain(Diagnosis, storedDiagnosis!, StringComparison.Ordinal);
        Assert.DoesNotContain("Secret prescriber note", storedNotes!, StringComparison.Ordinal);
        Assert.DoesNotContain("5mg", storedDose!, StringComparison.Ordinal);

        using (var scope = _factory.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ClinicDbContext>();

            var storedVisit = await context.PatientVisits.SingleAsync(candidate => candidate.Id == visit.Id);
            Assert.Equal(Diagnosis, storedVisit.Diagnosis);

            var stored = await context.Prescriptions
                .Include(candidate => candidate.Items)
                .SingleAsync(candidate => candidate.Id == prescription.Id);
            Assert.Equal("Secret prescriber note", stored.Notes);
            Assert.Equal("5mg", Assert.Single(stored.Items).Dose);
        }
    }

    private async Task<VisitDetails> RecordVisitAsync()
    {
        var response = await _doctor.PostAsJsonAsync(
            "/api/visits",
            new RecordVisitRequest(
                _patientId,
                _doctorId,
                DateTime.UtcNow,
                ChiefComplaint: "Headache for three days",
                Notes: VisitNotes,
                Diagnosis: Diagnosis));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var visit = await response.Content.ReadFromJsonAsync<VisitDetails>();
        Assert.NotNull(visit);

        return visit!;
    }

    private async Task<Guid> CreateAppointmentAsync(Guid patientId, bool cancelled)
    {
        using (var scope = _factory.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ClinicDbContext>();
            var startsAtUtc = DateTime.UtcNow.Date.AddDays(30).AddHours(10);

            var appointment = Appointment.Book(
                patientId,
                _doctorId,
                startsAtUtc,
                startsAtUtc.AddMinutes(30),
                reasonForVisit: null,
                bookedByUserId: _doctorUserId,
                DateTime.UtcNow);

            if (cancelled)
            {
                appointment.Cancel("cancelled by the test", DateTime.UtcNow);
            }

            context.Appointments.Add(appointment);
            await context.SaveChangesAsync();

            return appointment.Id;
        }
    }

    private async Task ArrangeAsync()
    {
        _secretary = await StaffClientAsync(RoleNames.Secretary, "secretary");
        _doctor = await StaffClientAsync(RoleNames.Doctor, "doctor");

        using (var scope = _factory.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ClinicDbContext>();
            var user = await context.Users.SingleAsync(candidate => candidate.Email == _doctorEmail);

            var doctor = Doctor.Create(user.Id, "Cardiology", $"LIC-{_reference}", DateTime.UtcNow);
            context.Doctors.Add(doctor);
            await context.SaveChangesAsync();

            _doctorId = doctor.Id;
            _doctorUserId = user.Id;
        }

        _doctorIds.Add(_doctorId);

        _patientId = await CreatePatientAsync("Salma", linkedToAccount: true);
        _patient = await PatientClientAsync();
        _otherPatientId = await CreatePatientAsync("Nadia", linkedToAccount: false);
    }

    private async Task<HttpClient> StaffClientAsync(string role, string label)
    {
        var email = $"{label}-{Guid.NewGuid():N}@clinic.test";
        await _factory.CreateUserAsync(email, TestApi.Password, role, $"{label} User");
        _emails.Add(email);

        if (role == RoleNames.Doctor)
        {
            _doctorEmail = email;
        }

        var login = await _factory.CreateClient().LoginAsync(email, TestApi.Password);
        Assert.False(login.MfaRequired, $"{role} must be able to sign in without MFA.");
        Assert.NotNull(login.Tokens);

        return _factory.CreateClientWithToken(login.Tokens!.AccessToken);
    }

    private async Task<Guid> CreatePatientAsync(string givenName, bool linkedToAccount)
    {
        var email = $"visit-patient-{Guid.NewGuid():N}@clinic.test";
        await _factory.CreateUserAsync(email, TestApi.Password, RoleNames.Patient, $"{givenName} Patient");
        _emails.Add(email);

        Guid patientId;
        using (var scope = _factory.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ClinicDbContext>();
            var user = await context.Users.SingleAsync(candidate => candidate.Email == email);

            var patient = Patient.Create(
                $"{_reference}-{_patientIds.Count + 1}",
                givenName,
                "Hassan",
                DateTime.UtcNow,
                linkedToAccount ? user.Id : (Guid?)null);

            context.Patients.Add(patient);
            await context.SaveChangesAsync();
            patientId = patient.Id;
        }

        _patientIds.Add(patientId);
        return patientId;
    }

    private async Task<HttpClient> PatientClientAsync()
    {
        using var scope = _factory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ClinicDbContext>();
        var user = await context.Users.SingleAsync(
            candidate => candidate.Patient != null && candidate.Patient.Id == _patientId);

        var login = await _factory.CreateClient().LoginAsync(user.Email!, TestApi.Password);
        Assert.False(login.MfaRequired);
        Assert.NotNull(login.Tokens);

        return _factory.CreateClientWithToken(login.Tokens!.AccessToken);
    }

    private static async Task<string?> ProblemCodeAsync(HttpResponseMessage response)
    {
        using var document = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonDocument>();

        return document!.RootElement.TryGetProperty("code", out var code)
            ? code.GetString()
            : null;
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

    public void Dispose()
    {
        using (var scope = _factory.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ClinicDbContext>();

            var prescriptions = context.Prescriptions
                .Where(candidate => _patientIds.Contains(candidate.PatientId))
                .ToList();

            var prescriptionIds = prescriptions.Select(candidate => candidate.Id).ToList();

            var items = context.PrescriptionItems.Where(item => prescriptionIds.Contains(item.PrescriptionId));
            context.PrescriptionItems.RemoveRange(items);
            context.Prescriptions.RemoveRange(prescriptions);

            var visits = context.PatientVisits.Where(candidate => _patientIds.Contains(candidate.PatientId));
            context.PatientVisits.RemoveRange(visits);

            var allergies = context.Allergies.Where(candidate => _patientIds.Contains(candidate.PatientId));
            context.Allergies.RemoveRange(allergies);

            var appointments = context.Appointments
                .Where(candidate => _doctorIds.Contains(candidate.DoctorId) ||
                                    _patientIds.Contains(candidate.PatientId));
            context.Appointments.RemoveRange(appointments);

            var doctors = context.Doctors.Where(candidate => _doctorIds.Contains(candidate.Id));
            context.Doctors.RemoveRange(doctors);

            var patients = context.Patients.Where(candidate => _patientIds.Contains(candidate.Id));
            context.Patients.RemoveRange(patients);

            context.SaveChanges();
        }

        foreach (var email in _emails)
        {
            _factory.DeleteUserAsync(email).GetAwaiter().GetResult();
        }
    }
}
