using ClinicManagement.Application.Common;
using ClinicManagement.Application.Common.Paging;

namespace ClinicManagement.Application.Billing;

public interface IBillingService
{
    Task<Result<PagedResult<InvoiceSummary>>> SearchInvoicesAsync(
        InvoiceSearchQuery query,
        CancellationToken cancellationToken);

    Task<Result<InvoiceDetails>> GetInvoiceAsync(Guid invoiceId, CancellationToken cancellationToken);

    Task<Result<InvoiceDetails>> CreateInvoiceAsync(
        CreateInvoiceRequest request,
        CancellationToken cancellationToken);

    Task<Result<InvoiceDetails>> IssueInvoiceAsync(Guid invoiceId, CancellationToken cancellationToken);

    Task<Result<InvoiceDetails>> RecordPaymentAsync(
        Guid invoiceId,
        RecordPaymentRequest request,
        CancellationToken cancellationToken);

    Task<Result<InvoiceDetails>> VoidInvoiceAsync(
        Guid invoiceId,
        VoidInvoiceRequest request,
        CancellationToken cancellationToken);

    Task<Result<PagedResult<InvoiceSummary>>> GetOwnInvoicesAsync(
        PageRequest page,
        CancellationToken cancellationToken);
}
