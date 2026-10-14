using ClinicManagement.Application.Abstractions;
using ClinicManagement.Application.Billing;
using ClinicManagement.Application.Common;
using ClinicManagement.Application.Common.Extensions;
using ClinicManagement.Application.Common.Paging;
using ClinicManagement.Domain.Auditing;
using ClinicManagement.Domain.Billing;
using ClinicManagement.Domain.Common;
using ClinicManagement.Domain.Users;
using ClinicManagement.Infrastructure.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ClinicManagement.Infrastructure.Billing;

public sealed class BillingService(
    ClinicDbContext dbContext,
    ICurrentUser currentUser,
    IAuditService auditService,
    TimeProvider timeProvider,
    IValidator<CreateInvoiceRequest> createValidator,
    IValidator<RecordPaymentRequest> paymentValidator,
    IValidator<VoidInvoiceRequest> voidValidator,
    IValidator<InvoiceSearchQuery> searchValidator,
    IValidator<PageRequest> pageValidator) : IBillingService
{
    public async Task<Result<PagedResult<InvoiceSummary>>> SearchInvoicesAsync(
        InvoiceSearchQuery query,
        CancellationToken cancellationToken)
    {
        var validation = await searchValidator.ValidateAndBindAsync(query, cancellationToken);
        if (validation.Failed)
        {
            return Result<PagedResult<InvoiceSummary>>.Failure(validation.Error!);
        }

        if (currentUser.IsInRole(RoleNames.Patient))
        {
            return Result<PagedResult<InvoiceSummary>>.Failure(
                ErrorCodes.Forbidden,
                "Patients may only view their own invoices.");
        }

        var page = new PageRequest { Page = query.Page, PageSize = query.PageSize };

        var invoices = dbContext.Invoices.AsNoTracking();

        if (query.PatientId is { } patientId)
        {
            invoices = invoices.Where(invoice => invoice.PatientId == patientId);
        }

        if (query.VisitId is { } visitId)
        {
            invoices = invoices.Where(invoice => invoice.VisitId == visitId);
        }

        if (query.Status is { } status)
        {
            invoices = invoices.Where(invoice => invoice.Status == status);
        }

        if (query.FromUtc is { } fromUtc)
        {
            invoices = invoices.Where(invoice => invoice.IssuedAtUtc >= fromUtc);
        }

        if (query.ToUtc is { } toUtc)
        {
            invoices = invoices.Where(invoice => invoice.IssuedAtUtc <= toUtc);
        }

        var totalCount = await invoices.CountAsync(cancellationToken);

        var items = await invoices
            .Include(invoice => invoice.Patient)
            .OrderByDescending(invoice => invoice.IssuedAtUtc)
            .ThenBy(invoice => invoice.Id)
            .Skip(page.Offset)
            .Take(page.PageSize)
            .ToListAsync(cancellationToken);

        return Result<PagedResult<InvoiceSummary>>.Success(
            PagedResult<InvoiceSummary>.Create(items.Select(MapSummary).ToArray(), page, totalCount));
    }

    public async Task<Result<InvoiceDetails>> GetInvoiceAsync(Guid invoiceId, CancellationToken cancellationToken)
    {
        var invoice = await LoadAsync(invoiceId, tracking: false, cancellationToken);

        if (invoice is null)
        {
            return Result<InvoiceDetails>.Failure(ErrorCodes.NotFound, "Invoice was not found.");
        }

        if (await IsBlockedFromPatientAsync(invoice.PatientId, cancellationToken))
        {
            return Result<InvoiceDetails>.Failure(ErrorCodes.Forbidden, "You may only view your own invoices.");
        }

        await auditService.RecordAsync(
            AuditAction.Viewed,
            nameof(Invoice),
            invoice.Id.ToString(),
            invoice.PatientId,
            cancellationToken: cancellationToken);

        return Result<InvoiceDetails>.Success(Map(invoice));
    }

    public async Task<Result<InvoiceDetails>> CreateInvoiceAsync(
        CreateInvoiceRequest request,
        CancellationToken cancellationToken)
    {
        var validation = await createValidator.ValidateAndBindAsync(request, cancellationToken);
        if (validation.Failed)
        {
            return Result<InvoiceDetails>.Failure(validation.Error!);
        }

        if (currentUser.IsInRole(RoleNames.Patient))
        {
            return Result<InvoiceDetails>.Failure(ErrorCodes.Forbidden, "Patients cannot create invoices.");
        }

        if (currentUser.UserId is not { } createdByUserId)
        {
            return Result<InvoiceDetails>.Failure(ErrorCodes.Forbidden, "No signed-in user was found.");
        }

        var patient = await dbContext.Patients.AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == request.PatientId, cancellationToken);

        if (patient is null)
        {
            return Result<InvoiceDetails>.Failure(ErrorCodes.NotFound, "Patient was not found.");
        }

        if (!patient.IsActive)
        {
            return Result<InvoiceDetails>.Failure(ErrorCodes.Conflict, "The patient record is inactive.");
        }

        if (request.VisitId is { } visitId)
        {
            var visitBelongsToPatient = await dbContext.PatientVisits.AsNoTracking()
                .AnyAsync(candidate => candidate.Id == visitId && candidate.PatientId == request.PatientId,
                    cancellationToken);

            if (!visitBelongsToPatient)
            {
                return Result<InvoiceDetails>.Failure(
                    ErrorCodes.ValidationFailed,
                    "The visit does not belong to that patient.");
            }
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;

        var invoice = Invoice.Create(
            BuildInvoiceNumber(now),
            request.PatientId,
            request.VisitId,
            request.Currency,
            request.Lines.Select(line => new InvoiceLineInput(line.Description, line.UnitPrice, line.Quantity)),
            createdByUserId,
            now);

        dbContext.Invoices.Add(invoice);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            return Result<InvoiceDetails>.Failure(ErrorCodes.Conflict, "That invoice number was just taken.");
        }

        await auditService.RecordAsync(
            AuditAction.Created,
            nameof(Invoice),
            invoice.Id.ToString(),
            invoice.PatientId,
            cancellationToken: cancellationToken);

        var created = await LoadAsync(invoice.Id, tracking: false, cancellationToken);
        return Result<InvoiceDetails>.Success(Map(created!));
    }

    public async Task<Result<InvoiceDetails>> IssueInvoiceAsync(Guid invoiceId, CancellationToken cancellationToken)
    {
        if (currentUser.IsInRole(RoleNames.Patient))
        {
            return Result<InvoiceDetails>.Failure(ErrorCodes.Forbidden, "Patients cannot issue invoices.");
        }

        var invoice = await LoadAsync(invoiceId, tracking: true, cancellationToken);

        if (invoice is null)
        {
            return Result<InvoiceDetails>.Failure(ErrorCodes.NotFound, "Invoice was not found.");
        }

        try
        {
            invoice.Issue(timeProvider.GetUtcNow().UtcDateTime);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DomainException exception)
        {
            return Result<InvoiceDetails>.Failure(exception.Code, exception.Message);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result<InvoiceDetails>.Failure(
                ErrorCodes.ConcurrencyConflict,
                "The invoice was modified by someone else. Reload it and try again.");
        }

        await auditService.RecordAsync(
            AuditAction.Updated,
            nameof(Invoice),
            invoice.Id.ToString(),
            invoice.PatientId,
            [nameof(Invoice.Status)],
            cancellationToken);

        return Result<InvoiceDetails>.Success(Map(invoice));
    }

    public async Task<Result<InvoiceDetails>> RecordPaymentAsync(
        Guid invoiceId,
        RecordPaymentRequest request,
        CancellationToken cancellationToken)
    {
        var validation = await paymentValidator.ValidateAndBindAsync(request, cancellationToken);
        if (validation.Failed)
        {
            return Result<InvoiceDetails>.Failure(validation.Error!);
        }

        if (currentUser.IsInRole(RoleNames.Patient))
        {
            return Result<InvoiceDetails>.Failure(ErrorCodes.Forbidden, "Patients cannot record payments.");
        }

        if (currentUser.UserId is not { } receivedByUserId)
        {
            return Result<InvoiceDetails>.Failure(ErrorCodes.Forbidden, "No signed-in user was found.");
        }

        var invoice = await LoadAsync(invoiceId, tracking: true, cancellationToken);

        if (invoice is null)
        {
            return Result<InvoiceDetails>.Failure(ErrorCodes.NotFound, "Invoice was not found.");
        }

        try
        {
            invoice.RecordPayment(
                request.Amount,
                request.Method,
                request.Reference,
                receivedByUserId,
                timeProvider.GetUtcNow().UtcDateTime);

            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DomainException exception)
        {
            return Result<InvoiceDetails>.Failure(exception.Code, exception.Message);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result<InvoiceDetails>.Failure(
                ErrorCodes.ConcurrencyConflict,
                "The invoice was modified by someone else. Reload it and try again.");
        }

        await auditService.RecordAsync(
            AuditAction.Updated,
            nameof(Payment),
            invoice.Id.ToString(),
            invoice.PatientId,
            [nameof(Invoice.AmountPaid), nameof(Invoice.Status)],
            cancellationToken);

        return Result<InvoiceDetails>.Success(Map(invoice));
    }

    public async Task<Result<InvoiceDetails>> VoidInvoiceAsync(
        Guid invoiceId,
        VoidInvoiceRequest request,
        CancellationToken cancellationToken)
    {
        var validation = await voidValidator.ValidateAndBindAsync(request, cancellationToken);
        if (validation.Failed)
        {
            return Result<InvoiceDetails>.Failure(validation.Error!);
        }

        if (currentUser.IsInRole(RoleNames.Patient))
        {
            return Result<InvoiceDetails>.Failure(ErrorCodes.Forbidden, "Patients cannot void invoices.");
        }

        var invoice = await LoadAsync(invoiceId, tracking: true, cancellationToken);

        if (invoice is null)
        {
            return Result<InvoiceDetails>.Failure(ErrorCodes.NotFound, "Invoice was not found.");
        }

        try
        {
            invoice.Void(request.Reason, timeProvider.GetUtcNow().UtcDateTime);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DomainException exception)
        {
            return Result<InvoiceDetails>.Failure(exception.Code, exception.Message);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result<InvoiceDetails>.Failure(
                ErrorCodes.ConcurrencyConflict,
                "The invoice was modified by someone else. Reload it and try again.");
        }

        await auditService.RecordAsync(
            AuditAction.Updated,
            nameof(Invoice),
            invoice.Id.ToString(),
            invoice.PatientId,
            [nameof(Invoice.Status), nameof(Invoice.VoidReason)],
            cancellationToken);

        return Result<InvoiceDetails>.Success(Map(invoice));
    }

    public async Task<Result<PagedResult<InvoiceSummary>>> GetOwnInvoicesAsync(
        PageRequest page,
        CancellationToken cancellationToken)
    {
        var pageValidation = await pageValidator.ValidateAndBindAsync(page, cancellationToken);
        if (pageValidation.Failed)
        {
            return Result<PagedResult<InvoiceSummary>>.Failure(pageValidation.Error!);
        }

        var patientId = await ResolveOwnPatientIdAsync(cancellationToken);
        if (patientId == Guid.Empty)
        {
            return Result<PagedResult<InvoiceSummary>>.Failure(
                ErrorCodes.NotFound,
                "No patient record is linked to this account.");
        }

        var invoices = dbContext.Invoices.AsNoTracking().Where(invoice => invoice.PatientId == patientId);

        var totalCount = await invoices.CountAsync(cancellationToken);

        var items = await invoices
            .Include(invoice => invoice.Patient)
            .OrderByDescending(invoice => invoice.IssuedAtUtc)
            .ThenBy(invoice => invoice.Id)
            .Skip(page.Offset)
            .Take(page.PageSize)
            .ToListAsync(cancellationToken);

        return Result<PagedResult<InvoiceSummary>>.Success(
            PagedResult<InvoiceSummary>.Create(items.Select(MapSummary).ToArray(), page, totalCount));
    }

    private async Task<Invoice?> LoadAsync(Guid invoiceId, bool tracking, CancellationToken cancellationToken)
    {
        var query = dbContext.Invoices
            .Include(invoice => invoice.Patient)
            .Include(invoice => invoice.Lines)
            .Include(invoice => invoice.Payments)
            .Where(invoice => invoice.Id == invoiceId);

        if (!tracking)
        {
            query = query.AsNoTracking();
        }

        return await query.FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<bool> IsBlockedFromPatientAsync(Guid patientId, CancellationToken cancellationToken)
    {
        if (!currentUser.IsInRole(RoleNames.Patient))
        {
            return false;
        }

        var ownPatientId = await ResolveOwnPatientIdAsync(cancellationToken);
        return ownPatientId == Guid.Empty || ownPatientId != patientId;
    }

    private async Task<Guid> ResolveOwnPatientIdAsync(CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Guid.Empty;
        }

        return await dbContext.Patients
            .AsNoTracking()
            .Where(patient => patient.ApplicationUserId == userId)
            .Select(patient => patient.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static string BuildInvoiceNumber(DateTime nowUtc) =>
        $"INV-{nowUtc:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";

    private static InvoiceSummary MapSummary(Invoice invoice) => new(
        invoice.Id,
        invoice.InvoiceNumber,
        invoice.PatientId,
        invoice.Patient?.FullName ?? string.Empty,
        invoice.VisitId,
        invoice.Status,
        invoice.Currency,
        invoice.TotalAmount,
        invoice.AmountPaid,
        invoice.TotalAmount - invoice.AmountPaid,
        invoice.IssuedAtUtc,
        invoice.CreatedAtUtc);

    private static InvoiceDetails Map(Invoice invoice) => new(
        invoice.Id,
        invoice.InvoiceNumber,
        invoice.PatientId,
        invoice.Patient?.FullName ?? string.Empty,
        invoice.VisitId,
        invoice.Status,
        invoice.Currency,
        invoice.TotalAmount,
        invoice.AmountPaid,
        invoice.TotalAmount - invoice.AmountPaid,
        invoice.IssuedAtUtc,
        invoice.VoidedAtUtc,
        invoice.VoidReason,
        invoice.CreatedByUserId,
        invoice.CreatedAtUtc,
        invoice.UpdatedAtUtc,
        invoice.Lines
            .OrderBy(line => line.Description, StringComparer.Ordinal)
            .Select(line => new InvoiceLineResponse(
                line.Id,
                line.Description,
                line.UnitPrice,
                line.Quantity,
                line.LineTotal))
            .ToArray(),
        invoice.Payments
            .OrderBy(payment => payment.PaidAtUtc)
            .ThenBy(payment => payment.Id)
            .Select(payment => new PaymentResponse(
                payment.Id,
                payment.Amount,
                payment.PaidAtUtc,
                payment.Method,
                payment.Reference,
                payment.ReceivedByUserId))
            .ToArray());

    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}
