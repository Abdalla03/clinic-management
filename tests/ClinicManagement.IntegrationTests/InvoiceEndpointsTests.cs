using System.Net;
using System.Net.Http.Json;
using ClinicManagement.Application.Billing;
using ClinicManagement.Application.Common.Paging;
using ClinicManagement.Application.Reports;
using ClinicManagement.Domain.Auditing;
using ClinicManagement.Domain.Billing;
using ClinicManagement.Domain.Common;
using ClinicManagement.Domain.Patients;
using ClinicManagement.Domain.Users;
using ClinicManagement.Infrastructure.Persistence;
using ClinicManagement.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ClinicManagement.IntegrationTests;

[Collection(ClinicApiCollection.Name)]
public sealed class InvoiceEndpointsTests : IDisposable
{
    private readonly ClinicApiFactory _factory;
    private readonly string _reference = $"BIL-{Guid.NewGuid().ToString("N")[..8]}";
    private readonly List<string> _emails = [];
    private readonly List<Guid> _doctorIds = [];
    private readonly List<Guid> _patientIds = [];

    private HttpClient _secretary = null!;
    private HttpClient _doctor = null!;
    private HttpClient _patient = null!;
    private Guid _patientId;
    private Guid _otherPatientId;
    private Guid _doctorId;
    private Guid _doctorUserId;
    private string? _doctorEmail;

    public InvoiceEndpointsTests(ClinicApiFactory factory)
    {
        _factory = factory;
    }

    [RequiresDockerFact]
    public async Task AnonymousCallerCannotCreateAnInvoice()
    {
        await ArrangeAsync();

        var response = await _factory.CreateClient().PostAsJsonAsync(
            "/api/invoices",
            new CreateInvoiceRequest(_patientId, [new InvoiceLineRequest("Consultation", 150m)]));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [RequiresDockerFact]
    public async Task OnlySecretariesCanCreateInvoices()
    {
        await ArrangeAsync();

        var byDoctor = await _doctor.PostAsJsonAsync(
            "/api/invoices",
            new CreateInvoiceRequest(_patientId, [new InvoiceLineRequest("Consultation", 150m)]));
        Assert.Equal(HttpStatusCode.Forbidden, byDoctor.StatusCode);

        var byPatient = await _patient.PostAsJsonAsync(
            "/api/invoices",
            new CreateInvoiceRequest(_patientId, [new InvoiceLineRequest("Consultation", 150m)]));
        Assert.Equal(HttpStatusCode.Forbidden, byPatient.StatusCode);

        var bySecretary = await _secretary.PostAsJsonAsync(
            "/api/invoices",
            new CreateInvoiceRequest(_patientId, [new InvoiceLineRequest("Consultation", 150m)]));

        Assert.Equal(HttpStatusCode.Created, bySecretary.StatusCode);

        var invoice = await bySecretary.Content.ReadFromJsonAsync<InvoiceDetails>();
        Assert.Equal(InvoiceStatus.Draft, invoice!.Status);
        Assert.Equal(150m, invoice.TotalAmount);
        Assert.Equal(0m, invoice.AmountPaid);
        Assert.Equal(_patientId, invoice.PatientId);
        Assert.False(string.IsNullOrWhiteSpace(invoice.InvoiceNumber));
        Assert.StartsWith("INV-", invoice.InvoiceNumber);
    }

    [RequiresDockerFact]
    public async Task InvoiceLifecycleRunsFromDraftThroughToPaid()
    {
        await ArrangeAsync();

        var created = await _secretary.PostAsJsonAsync(
            "/api/invoices",
            new CreateInvoiceRequest(
                _patientId,
                [new InvoiceLineRequest("Consultation", 200m, 1), new InvoiceLineRequest("Lab work", 50m, 2)],
                Currency: "EGP"));

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var invoice = (await created.Content.ReadFromJsonAsync<InvoiceDetails>())!;
        Assert.Equal(300m, invoice.TotalAmount);
        Assert.Equal(InvoiceStatus.Draft, invoice.Status);

        var issue = await _secretary.PostAsync($"/api/invoices/{invoice.Id}/issue", null);
        Assert.Equal(HttpStatusCode.OK, issue.StatusCode);
        var issued = await issue.Content.ReadFromJsonAsync<InvoiceDetails>();
        Assert.Equal(InvoiceStatus.Issued, issued!.Status);
        Assert.True(issued.IssuedAtUtc > DateTime.UnixEpoch);

        var partial = await _secretary.PostAsJsonAsync(
            $"/api/invoices/{invoice.Id}/payments",
            new RecordPaymentRequest(120m, PaymentMethod.Cash));
        Assert.Equal(HttpStatusCode.OK, partial.StatusCode);
        var partiallyPaid = await partial.Content.ReadFromJsonAsync<InvoiceDetails>();
        Assert.Equal(InvoiceStatus.PartiallyPaid, partiallyPaid!.Status);
        Assert.Equal(120m, partiallyPaid.AmountPaid);
        Assert.Equal(180m, partiallyPaid.OutstandingAmount);

        var settle = await _secretary.PostAsJsonAsync(
            $"/api/invoices/{invoice.Id}/payments",
            new RecordPaymentRequest(180m, PaymentMethod.Card, "POS-7"));
        Assert.Equal(HttpStatusCode.OK, settle.StatusCode);
        var paid = await settle.Content.ReadFromJsonAsync<InvoiceDetails>();
        Assert.Equal(InvoiceStatus.Paid, paid!.Status);
        Assert.Equal(300m, paid.AmountPaid);
        Assert.Equal(0m, paid.OutstandingAmount);

        var payments = paid.Payments.OrderBy(payment => payment.PaidAtUtc).ToArray();
        Assert.Equal(2, payments.Length);
        Assert.Equal(120m, payments[0].Amount);
        Assert.Equal(PaymentMethod.Cash, payments[0].Method);
        Assert.Equal(180m, payments[1].Amount);
        Assert.Equal("POS-7", payments[1].Reference);

        using var scope = _factory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ClinicDbContext>();
        var entries = await context.AuditLogEntries
            .Where(entry => entry.PatientId == _patientId && entry.EntityName == nameof(Invoice))
            .ToListAsync();

        Assert.Contains(entries, entry => entry.Action == AuditAction.Created && entry.EntityId == invoice.Id.ToString());
        Assert.Contains(entries, entry => entry.Action == AuditAction.Updated && entry.EntityId == invoice.Id.ToString());
    }

    [RequiresDockerFact]
    public async Task PaymentAndIssuingRulesAreEnforced()
    {
        await ArrangeAsync();

        var invoice = await CreateInvoiceAsync();

        var payDraft = await _secretary.PostAsJsonAsync(
            $"/api/invoices/{invoice.Id}/payments",
            new RecordPaymentRequest(10m, PaymentMethod.Cash));
        Assert.Equal(HttpStatusCode.Conflict, payDraft.StatusCode);
        Assert.Equal(ErrorCodes.InvalidStateTransition, await ProblemCodeAsync(payDraft));

        (await _secretary.PostAsync($"/api/invoices/{invoice.Id}/issue", null)).EnsureSuccessStatusCode();

        var issueAgain = await _secretary.PostAsync($"/api/invoices/{invoice.Id}/issue", null);
        Assert.Equal(HttpStatusCode.Conflict, issueAgain.StatusCode);
        Assert.Equal(ErrorCodes.InvalidStateTransition, await ProblemCodeAsync(issueAgain));

        var zero = await _secretary.PostAsJsonAsync(
            $"/api/invoices/{invoice.Id}/payments",
            new RecordPaymentRequest(0m, PaymentMethod.Cash));
        Assert.Equal(HttpStatusCode.BadRequest, zero.StatusCode);
        Assert.Equal(ErrorCodes.ValidationFailed, await ProblemCodeAsync(zero));

        var over = await _secretary.PostAsJsonAsync(
            $"/api/invoices/{invoice.Id}/payments",
            new RecordPaymentRequest(9999m, PaymentMethod.Cash));
        Assert.Equal(HttpStatusCode.BadRequest, over.StatusCode);
        Assert.Equal(ErrorCodes.ValidationFailed, await ProblemCodeAsync(over));

        var emptyLines = await _secretary.PostAsJsonAsync(
            "/api/invoices",
            new CreateInvoiceRequest(_patientId, []));
        Assert.Equal(HttpStatusCode.BadRequest, emptyLines.StatusCode);
        Assert.Equal(ErrorCodes.ValidationFailed, await ProblemCodeAsync(emptyLines));
    }

    [RequiresDockerFact]
    public async Task PatientsCanOnlyReadTheirOwnInvoices()
    {
        await ArrangeAsync();

        var mine = await CreateInvoiceAsync();
        (await _secretary.PostAsync($"/api/invoices/{mine.Id}/issue", null)).EnsureSuccessStatusCode();

        var foreignPatientId = _otherPatientId;
        var foreign = await _secretary.PostAsJsonAsync(
            "/api/invoices",
            new CreateInvoiceRequest(foreignPatientId, [new InvoiceLineRequest("Consultation", 90m)]));
        Assert.Equal(HttpStatusCode.Created, foreign.StatusCode);
        var foreignInvoice = (await foreign.Content.ReadFromJsonAsync<InvoiceDetails>())!;

        (await _patient.GetAsync($"/api/invoices/{mine.Id}")).EnsureSuccessStatusCode();

        var denied = await _patient.GetAsync($"/api/invoices/{foreignInvoice.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);

        var search = await _patient.GetAsync("/api/invoices");
        Assert.Equal(HttpStatusCode.Forbidden, search.StatusCode);

        var list = await _patient.GetAsync("/api/invoices/mine");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);

        var page = await list.Content.ReadFromJsonAsync<PagedResult<InvoiceSummary>>();
        Assert.Contains(page!.Items, item => item.Id == mine.Id);
        Assert.DoesNotContain(page.Items, item => item.Id == foreignInvoice.Id);
    }

    [RequiresDockerFact]
    public async Task InvoicesCanReferenceVisitsOfThatPatientOnly()
    {
        await ArrangeAsync();

        var visitId = await RecordVisitAsync(_patientId);

        var linked = await _secretary.PostAsJsonAsync(
            "/api/invoices",
            new CreateInvoiceRequest(
                _patientId,
                [new InvoiceLineRequest("Consultation after visit", 120m)],
                VisitId: visitId));

        Assert.Equal(HttpStatusCode.Created, linked.StatusCode);
        var invoice = await linked.Content.ReadFromJsonAsync<InvoiceDetails>();
        Assert.Equal(visitId, invoice!.VisitId);

        var otherVisitId = await RecordVisitAsync(_otherPatientId);

        var mismatched = await _secretary.PostAsJsonAsync(
            "/api/invoices",
            new CreateInvoiceRequest(
                _patientId,
                [new InvoiceLineRequest("Consultation", 120m)],
                VisitId: otherVisitId));

        Assert.Equal(HttpStatusCode.BadRequest, mismatched.StatusCode);
        Assert.Equal(ErrorCodes.ValidationFailed, await ProblemCodeAsync(mismatched));
    }

    [RequiresDockerFact]
    public async Task VoidRulesAreEnforced()
    {
        await ArrangeAsync();

        var open = await CreateInvoiceAsync();
        (await _secretary.PostAsync($"/api/invoices/{open.Id}/issue", null)).EnsureSuccessStatusCode();

        var voidOpen = await _secretary.PostAsJsonAsync(
            $"/api/invoices/{open.Id}/void",
            new VoidInvoiceRequest("Patient cancelled the procedure"));
        Assert.Equal(HttpStatusCode.OK, voidOpen.StatusCode);

        var voided = await voidOpen.Content.ReadFromJsonAsync<InvoiceDetails>();
        Assert.Equal(InvoiceStatus.Void, voided!.Status);
        Assert.Equal("Patient cancelled the procedure", voided.VoidReason);
        Assert.NotNull(voided.VoidedAtUtc);

        var voidAgain = await _secretary.PostAsJsonAsync(
            $"/api/invoices/{open.Id}/void",
            new VoidInvoiceRequest("again"));
        Assert.Equal(HttpStatusCode.Conflict, voidAgain.StatusCode);
        Assert.Equal(ErrorCodes.InvalidStateTransition, await ProblemCodeAsync(voidAgain));

        var paid = await CreateInvoiceAsync();
        (await _secretary.PostAsync($"/api/invoices/{paid.Id}/issue", null)).EnsureSuccessStatusCode();
        (await _secretary.PostAsJsonAsync(
            $"/api/invoices/{paid.Id}/payments",
            new RecordPaymentRequest(150m, PaymentMethod.Cash))).EnsureSuccessStatusCode();

        var voidPaid = await _secretary.PostAsJsonAsync(
            $"/api/invoices/{paid.Id}/void",
            new VoidInvoiceRequest("changed my mind"));
        Assert.Equal(HttpStatusCode.Conflict, voidPaid.StatusCode);
        Assert.Equal(ErrorCodes.InvalidStateTransition, await ProblemCodeAsync(voidPaid));

        var noReason = await _secretary.PostAsJsonAsync(
            $"/api/invoices/{paid.Id}/void",
            new VoidInvoiceRequest(""));
        Assert.Equal(HttpStatusCode.BadRequest, noReason.StatusCode);
        Assert.Equal(ErrorCodes.ValidationFailed, await ProblemCodeAsync(noReason));
    }

    [RequiresDockerFact]
    public async Task ReportsAggregateRevenueOutstandingAndVisits()
    {
        await ArrangeAsync();

        var paid = await CreateInvoiceAsync(lines: [new InvoiceLineRequest("Consultation", 300m)]);
        (await _secretary.PostAsync($"/api/invoices/{paid.Id}/issue", null)).EnsureSuccessStatusCode();
        (await _secretary.PostAsJsonAsync(
            $"/api/invoices/{paid.Id}/payments",
            new RecordPaymentRequest(300m, PaymentMethod.Cash))).EnsureSuccessStatusCode();

        var partial = await CreateInvoiceAsync(lines: [new InvoiceLineRequest("Lab work", 200m)]);
        (await _secretary.PostAsync($"/api/invoices/{partial.Id}/issue", null)).EnsureSuccessStatusCode();
        (await _secretary.PostAsJsonAsync(
            $"/api/invoices/{partial.Id}/payments",
            new RecordPaymentRequest(50m, PaymentMethod.Card))).EnsureSuccessStatusCode();

        var open = await CreateInvoiceAsync(lines: [new InvoiceLineRequest("X-ray", 100m)]);
        (await _secretary.PostAsync($"/api/invoices/{open.Id}/issue", null)).EnsureSuccessStatusCode();

        await RecordVisitAsync(_patientId);
        await RecordVisitAsync(_patientId);

        var fromUtc = DateTime.UtcNow.AddDays(-1).ToString("O");
        var toUtc = DateTime.UtcNow.AddDays(1).ToString("O");

        var revenueResponse = await _secretary.GetAsync(
            $"/api/reports/revenue?fromUtc={fromUtc}&toUtc={toUtc}");
        Assert.Equal(HttpStatusCode.OK, revenueResponse.StatusCode);

        var revenue = await revenueResponse.Content.ReadFromJsonAsync<RevenueReport>();
        Assert.Equal(350m, revenue!.TotalCollected);
        Assert.Equal(2, revenue.InvoiceCount);
        Assert.Equal(2, revenue.PaymentCount);
        Assert.Equal(2, revenue.ByMethod.Count);
        Assert.Contains(revenue.ByMethod, entry => entry.Method == PaymentMethod.Cash && entry.Amount == 300m);
        Assert.Contains(revenue.ByMethod, entry => entry.Method == PaymentMethod.Card && entry.Amount == 50m);

        var outstandingResponse = await _secretary.GetAsync(
            $"/api/reports/outstanding?fromUtc={fromUtc}&toUtc={toUtc}");
        Assert.Equal(HttpStatusCode.OK, outstandingResponse.StatusCode);

        var outstanding = await outstandingResponse.Content.ReadFromJsonAsync<OutstandingReport>();
        Assert.Equal(250m, outstanding!.TotalOutstanding);
        var byPatient = Assert.Single(outstanding.ByPatient);
        Assert.Equal(_patientId, byPatient.PatientId);
        Assert.Equal(2, byPatient.InvoiceCount);

        var visitsResponse = await _secretary.GetAsync(
            $"/api/reports/visits?fromUtc={fromUtc}&toUtc={toUtc}");
        Assert.Equal(HttpStatusCode.OK, visitsResponse.StatusCode);

        var visitReport = await visitsResponse.Content.ReadFromJsonAsync<VisitReport>();
        Assert.Equal(2, visitReport!.TotalVisits);
        var byDoctor = Assert.Single(visitReport.ByDoctor);
        Assert.Equal(_doctorId, byDoctor.DoctorId);
        Assert.Equal(2, byDoctor.VisitCount);
        Assert.False(string.IsNullOrWhiteSpace(byDoctor.DoctorName));

        var byDoctorUser = await _doctor.GetAsync(
            $"/api/reports/revenue?fromUtc={fromUtc}&toUtc={toUtc}");
        Assert.Equal(HttpStatusCode.Forbidden, byDoctorUser.StatusCode);

        var byPatientUser = await _patient.GetAsync(
            $"/api/reports/revenue?fromUtc={fromUtc}&toUtc={toUtc}");
        Assert.Equal(HttpStatusCode.Forbidden, byPatientUser.StatusCode);
    }

    private async Task<InvoiceDetails> CreateInvoiceAsync(IReadOnlyList<InvoiceLineRequest>? lines = null)
    {
        var response = await _secretary.PostAsJsonAsync(
            "/api/invoices",
            new CreateInvoiceRequest(
                _patientId,
                lines ?? [new InvoiceLineRequest("Consultation", 150m)]));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var invoice = await response.Content.ReadFromJsonAsync<InvoiceDetails>();
        Assert.NotNull(invoice);

        return invoice!;
    }

    private async Task<Guid> RecordVisitAsync(Guid patientId)
    {
        using var scope = _factory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ClinicDbContext>();

        var visit = PatientVisit.Record(
            patientId,
            _doctorId,
            appointmentId: null,
            DateTime.UtcNow,
            chiefComplaint: "Routine check-up",
            notes: null,
            diagnosis: "Healthy",
            _doctorUserId,
            DateTime.UtcNow);

        context.PatientVisits.Add(visit);
        await context.SaveChangesAsync();

        return visit.Id;
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

        _patientId = await CreatePatientAsync("Salma");
        _patient = await PatientClientAsync();
        _otherPatientId = await CreatePatientAsync("Nadia");
    }

    private async Task<HttpClient> StaffClientAsync(string role, string label)
    {
        var email = $"{label}-bil-{Guid.NewGuid():N}@clinic.test";
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

    private async Task<Guid> CreatePatientAsync(string givenName)
    {
        var email = $"billing-patient-{Guid.NewGuid():N}@clinic.test";
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
                user.Id);

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

    public void Dispose()
    {
        using (var scope = _factory.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ClinicDbContext>();

            var invoices = context.Invoices
                .Where(candidate => _patientIds.Contains(candidate.PatientId))
                .ToList();

            var invoiceIds = invoices.Select(candidate => candidate.Id).ToList();

            var payments = context.Payments.Where(candidate => invoiceIds.Contains(candidate.InvoiceId));
            context.Payments.RemoveRange(payments);

            var lines = context.InvoiceLines.Where(candidate => invoiceIds.Contains(candidate.InvoiceId));
            context.InvoiceLines.RemoveRange(lines);

            context.Invoices.RemoveRange(invoices);

            var visits = context.PatientVisits.Where(candidate => _patientIds.Contains(candidate.PatientId));
            context.PatientVisits.RemoveRange(visits);

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
