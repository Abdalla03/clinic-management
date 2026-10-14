using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ClinicManagement.Application.Common.Paging;
using ClinicManagement.Application.Patients;
using ClinicManagement.Domain.Auditing;
using ClinicManagement.Domain.Patients;
using ClinicManagement.Domain.Users;
using ClinicManagement.Infrastructure.Persistence;
using ClinicManagement.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ClinicManagement.IntegrationTests;

[Collection(ClinicApiCollection.Name)]
public sealed class PatientEndpointsTests : IDisposable
{
    private readonly ClinicApiFactory _factory;
    private readonly string _medicalRecordPrefix = $"MRN-{Guid.NewGuid().ToString("N")[..8]}";
    private readonly List<string> _emails = [];
    private string? _lastStaffEmail;

    public PatientEndpointsTests(ClinicApiFactory factory)
    {
        _factory = factory;
    }

    [RequiresDockerFact]
    public async Task AnonymousCaller_CannotReachThePatientDirectory()
    {
        var response = await _factory.CreateClient().GetAsync("/api/patients");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [RequiresDockerFact]
    public async Task SecretaryCanCreateAPatientAndReadItBack()
    {
        var secretary = await StaffClientAsync(RoleNames.Secretary);
        var medicalRecordNumber = NewMedicalRecordNumber();

        var create = await secretary.PostAsJsonAsync(
            "/api/patients",
            new CreatePatientRequest(
                medicalRecordNumber,
                "Salma",
                "Hassan",
                PhoneNumber: "+201001112233",
                DateOfBirth: new DateOnly(1990, 4, 2)));

        Assert.Equal(HttpStatusCode.Created, create.StatusCode);

        var created = await create.Content.ReadFromJsonAsync<PatientDetails>();
        Assert.NotNull(created);
        Assert.Equal(medicalRecordNumber, created!.MedicalRecordNumber);

        var fetched = await secretary.GetAsync($"/api/patients/{created.Id}");
        Assert.Equal(HttpStatusCode.OK, fetched.StatusCode);

        var details = await fetched.Content.ReadFromJsonAsync<PatientDetails>();
        Assert.Equal("+201001112233", details!.PhoneNumber);
        Assert.Equal(new DateOnly(1990, 4, 2), details.DateOfBirth);
        Assert.True(details.IsActive);

        using var scope = _factory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ClinicDbContext>();
        var auditEntries = await context.AuditLogEntries
            .Where(entry => entry.PatientId == created.Id)
            .ToListAsync();

        Assert.Contains(auditEntries, entry => entry.Action == AuditAction.Created && entry.EntityName == "Patient");
    }

    [RequiresDockerFact]
    public async Task SearchFindsAPatientByMedicalRecordNumber()
    {
        var secretary = await StaffClientAsync(RoleNames.Secretary);
        var medicalRecordNumber = NewMedicalRecordNumber();

        var create = await secretary.PostAsJsonAsync(
            "/api/patients",
            new CreatePatientRequest(medicalRecordNumber, "Nadia", "Farouk"));
        create.EnsureSuccessStatusCode();
        var created = await create.Content.ReadFromJsonAsync<PatientDetails>();

        var search = await secretary.GetAsync($"/api/patients?query={medicalRecordNumber}");
        search.EnsureSuccessStatusCode();

        var page = await search.Content.ReadFromJsonAsync<PagedResult<PatientSummary>>();
        Assert.NotNull(page);
        Assert.Contains(page!.Items, item => item.Id == created!.Id);
        Assert.Contains(page.Items, item => item.MedicalRecordNumber == medicalRecordNumber);
    }

    [RequiresDockerFact]
    public async Task CreatingTwoPatientsWithTheSameMedicalRecordNumber_ReturnsConflict()
    {
        var secretary = await StaffClientAsync(RoleNames.Secretary);
        var medicalRecordNumber = NewMedicalRecordNumber();

        var first = await secretary.PostAsJsonAsync(
            "/api/patients",
            new CreatePatientRequest(medicalRecordNumber, "Nadia", "Farouk"));
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var second = await secretary.PostAsJsonAsync(
            "/api/patients",
            new CreatePatientRequest(medicalRecordNumber, "Other", "Person"));
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [RequiresDockerFact]
    public async Task CreatingAPatientWithInvalidInput_ReturnsProblemDetailsWithFieldErrors()
    {
        var secretary = await StaffClientAsync(RoleNames.Secretary);

        var response = await secretary.PostAsJsonAsync(
            "/api/patients",
            new CreatePatientRequest(string.Empty, string.Empty, string.Empty));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("validation.failed", problem.RootElement.GetProperty("code").GetString());
        Assert.True(problem.RootElement.TryGetProperty("errors", out _));
    }

    [RequiresDockerFact]
    public async Task Search_IsPaginatedAndFiltersByActivity()
    {
        var secretary = await StaffClientAsync(RoleNames.Secretary);

        var created = new List<PatientDetails>();
        foreach (var suffix in new[] { "A", "B", "C" })
        {
            var response = await secretary.PostAsJsonAsync(
                "/api/patients",
                new CreatePatientRequest($"{NewMedicalRecordNumber()}{suffix}", suffix, "Paginated"));
            response.EnsureSuccessStatusCode();
            created.Add((await response.Content.ReadFromJsonAsync<PatientDetails>())!);
        }

        var firstPage = await secretary.GetAsync(
            $"/api/patients?query={_medicalRecordPrefix}&page=1&pageSize=2");
        firstPage.EnsureSuccessStatusCode();
        var page = await firstPage.Content.ReadFromJsonAsync<PagedResult<PatientSummary>>();

        Assert.Equal(2, page!.Items.Count);
        Assert.Equal(3, page.TotalCount);
        Assert.Equal(2, page.TotalPages);
        Assert.True(page.HasNextPage);
        Assert.False(page.HasPreviousPage);

        var secondPage = await secretary.GetAsync(
            $"/api/patients?query={_medicalRecordPrefix}&page=2&pageSize=2");
        secondPage.EnsureSuccessStatusCode();
        var tail = await secondPage.Content.ReadFromJsonAsync<PagedResult<PatientSummary>>();

        Assert.Single(tail!.Items);
        Assert.True(tail.HasPreviousPage);
        Assert.False(tail.HasNextPage);

        await secretary.DeleteAsync($"/api/patients/{created[0].Id}");

        var activeOnly = await secretary.GetAsync(
            $"/api/patients?query={_medicalRecordPrefix}&isActive=true");
        activeOnly.EnsureSuccessStatusCode();
        var activePage = await activeOnly.Content.ReadFromJsonAsync<PagedResult<PatientSummary>>();

        Assert.DoesNotContain(activePage!.Items, item => item.Id == created[0].Id);
        Assert.Equal(2, activePage.TotalCount);
    }

    [RequiresDockerFact]
    public async Task DoctorCanRecordMedicalHistoryThatThePatientCanRead()
    {
        var doctor = await StaffClientAsync(RoleNames.Doctor);
        var medicalRecordNumber = NewMedicalRecordNumber();
        var (patientClient, patientId) = await PatientClientAsync(medicalRecordNumber);

        var add = await doctor.PostAsJsonAsync(
            $"/api/patients/{patientId}/medical-history",
            new AddMedicalHistoryRequest("Type 2 diabetes", new DateOnly(2019, 6, 1), "Managed with metformin"));

        Assert.Equal(HttpStatusCode.Created, add.StatusCode);

        var history = await patientClient.GetAsync("/api/patients/me/medical-history");
        history.EnsureSuccessStatusCode();

        var page = await history.Content.ReadFromJsonAsync<PagedResult<MedicalHistoryEntryResponse>>();
        Assert.Contains(page!.Items, entry => entry.ConditionName == "Type 2 diabetes");

        var staffView = await doctor.GetAsync($"/api/patients/{patientId}/medical-history");
        staffView.EnsureSuccessStatusCode();
        var staffPage = await staffView.Content.ReadFromJsonAsync<PagedResult<MedicalHistoryEntryResponse>>();
        Assert.Contains(staffPage!.Items, entry => entry.Notes == "Managed with metformin");
    }

    [RequiresDockerFact]
    public async Task PatientCanReadTheirOwnRecordButNothingElse()
    {
        var medicalRecordNumber = NewMedicalRecordNumber();
        var (patientClient, patientId) = await PatientClientAsync(medicalRecordNumber);

        var own = await patientClient.GetAsync("/api/patients/me");
        Assert.Equal(HttpStatusCode.OK, own.StatusCode);
        var details = await own.Content.ReadFromJsonAsync<PatientDetails>();
        Assert.Equal(medicalRecordNumber, details!.MedicalRecordNumber);

        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await patientClient.GetAsync("/api/patients")).StatusCode);
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await patientClient.GetAsync($"/api/patients/{patientId}")).StatusCode);
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await patientClient.GetAsync($"/api/patients/{Guid.NewGuid()}")).StatusCode);
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await patientClient.PostAsJsonAsync(
                "/api/patients",
                new CreatePatientRequest(NewMedicalRecordNumber(), "Sneaky", "Patient"))).StatusCode);
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await patientClient.PutAsJsonAsync(
                $"/api/patients/{patientId}",
                new UpdatePatientRequest("Sneaky", "Patient"))).StatusCode);
    }

    [RequiresDockerFact]
    public async Task DoctorCannotDeactivateAPatient()
    {
        var doctor = await StaffClientAsync(RoleNames.Doctor);
        var secretary = await StaffClientAsync(RoleNames.Secretary);

        var medicalRecordNumber = NewMedicalRecordNumber();
        var create = await secretary.PostAsJsonAsync(
            "/api/patients",
            new CreatePatientRequest(medicalRecordNumber, "Mona", "Said"));
        create.EnsureSuccessStatusCode();
        var patient = await create.Content.ReadFromJsonAsync<PatientDetails>();

        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await doctor.DeleteAsync($"/api/patients/{patient!.Id}")).StatusCode);
    }

    [RequiresDockerFact]
    public async Task SecretaryCanDeactivateAPatient()
    {
        var secretary = await StaffClientAsync(RoleNames.Secretary);
        var medicalRecordNumber = NewMedicalRecordNumber();

        var create = await secretary.PostAsJsonAsync(
            "/api/patients",
            new CreatePatientRequest(medicalRecordNumber, "Mona", "Said"));
        create.EnsureSuccessStatusCode();
        var patient = await create.Content.ReadFromJsonAsync<PatientDetails>();

        var remove = await secretary.DeleteAsync($"/api/patients/{patient!.Id}");
        Assert.Equal(HttpStatusCode.NoContent, remove.StatusCode);

        var fetched = await secretary.GetAsync($"/api/patients/{patient.Id}");
        fetched.EnsureSuccessStatusCode();
        var details = await fetched.Content.ReadFromJsonAsync<PatientDetails>();
        Assert.False(details!.IsActive);

        var again = await secretary.DeleteAsync($"/api/patients/{patient.Id}");
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
    }

    [RequiresDockerFact]
    public async Task ReadingAPatientWritesAnAuditEntryAttributedToTheCaller()
    {
        var doctor = await StaffClientAsync(RoleNames.Doctor);
        var doctorEmail = _lastStaffEmail!;
        var medicalRecordNumber = NewMedicalRecordNumber();
        var (_, patientId) = await PatientClientAsync(medicalRecordNumber);

        var response = await doctor.GetAsync($"/api/patients/{patientId}");
        response.EnsureSuccessStatusCode();

        using var scope = _factory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ClinicDbContext>();
        var doctorUserId = await context.Users
            .Where(user => user.Email == doctorEmail)
            .Select(user => user.Id)
            .SingleAsync();

        var entries = await context.AuditLogEntries
            .Where(entry => entry.PatientId == patientId)
            .OrderBy(entry => entry.Id)
            .ToListAsync();

        Assert.Contains(entries, entry => entry.Action == AuditAction.Viewed && entry.EntityName == "Patient");

        var view = entries.Single(entry => entry.Action == AuditAction.Viewed && entry.EntityName == "Patient");
        Assert.Equal(doctorUserId, view.ActorUserId);
        Assert.Equal(RoleNames.Doctor, view.ActorRole);
        Assert.False(string.IsNullOrWhiteSpace(view.EntryHash));
    }

    [RequiresDockerFact]
    public async Task PatientAccessToTheirOwnRecordIsAudited()
    {
        var medicalRecordNumber = NewMedicalRecordNumber();
        var (patientClient, patientId) = await PatientClientAsync(medicalRecordNumber);

        (await patientClient.GetAsync("/api/patients/me")).EnsureSuccessStatusCode();

        using var scope = _factory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ClinicDbContext>();
        var entries = await context.AuditLogEntries
            .Where(entry => entry.PatientId == patientId && entry.Action == AuditAction.Viewed)
            .ToListAsync();

        Assert.NotEmpty(entries);
        Assert.All(entries, entry => Assert.Equal(nameof(Patient), entry.EntityName));
    }

    private string NewMedicalRecordNumber() => $"{_medicalRecordPrefix}-{Guid.NewGuid().ToString("N")[..6]}";

    private async Task<HttpClient> StaffClientAsync(string role)
    {
        var email = $"{role.ToLowerInvariant()}-{Guid.NewGuid():N}@clinic.test";
        await _factory.CreateUserAsync(email, TestApi.Password, role);
        _emails.Add(email);
        _lastStaffEmail = email;

        var login = await _factory.CreateClient().LoginAsync(email, TestApi.Password);
        Assert.False(login.MfaRequired, $"{role} must be able to sign in without MFA.");
        Assert.NotNull(login.Tokens);

        return _factory.CreateClientWithToken(login.Tokens!.AccessToken);
    }

    private async Task<(HttpClient Client, Guid PatientId)> PatientClientAsync(string medicalRecordNumber)
    {
        var email = $"patient-{Guid.NewGuid():N}@clinic.test";
        await _factory.CreateUserAsync(email, TestApi.Password, RoleNames.Patient);
        _emails.Add(email);

        Guid patientId;
        using (var scope = _factory.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ClinicDbContext>();
            var user = await context.Users.SingleAsync(candidate => candidate.Email == email);
            var patient = Patient.Create(medicalRecordNumber, "Salma", "Hassan", DateTime.UtcNow, user.Id);
            context.Patients.Add(patient);
            await context.SaveChangesAsync();
            patientId = patient.Id;
        }

        var login = await _factory.CreateClient().LoginAsync(email, TestApi.Password);
        Assert.False(login.MfaRequired);
        Assert.NotNull(login.Tokens);

        return (_factory.CreateClientWithToken(login.Tokens!.AccessToken), patientId);
    }

    public void Dispose()
    {
        using (var scope = _factory.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ClinicDbContext>();

            var patientIds = context.Patients
                .Where(patient => patient.MedicalRecordNumber.StartsWith(_medicalRecordPrefix))
                .Select(patient => patient.Id)
                .ToList();

            var histories = context.MedicalHistoryEntries
                .Where(entry => patientIds.Contains(entry.PatientId));
            context.MedicalHistoryEntries.RemoveRange(histories);

            var patients = context.Patients.Where(patient => patientIds.Contains(patient.Id));
            context.Patients.RemoveRange(patients);

            context.SaveChanges();
        }

        foreach (var email in _emails)
        {
            _factory.DeleteUserAsync(email).GetAwaiter().GetResult();
        }
    }
}
