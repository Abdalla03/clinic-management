using ClinicManagement.Application.Common.Paging;
using ClinicManagement.Domain.Billing;

namespace ClinicManagement.Application.Billing;

public sealed record InvoiceLineResponse(
    Guid Id,
    string Description,
    decimal UnitPrice,
    int Quantity,
    decimal LineTotal);

public sealed record PaymentResponse(
    Guid Id,
    decimal Amount,
    DateTime PaidAtUtc,
    PaymentMethod Method,
    string? Reference,
    Guid ReceivedByUserId);

public sealed record InvoiceSummary(
    Guid Id,
    string InvoiceNumber,
    Guid PatientId,
    string PatientName,
    Guid? VisitId,
    InvoiceStatus Status,
    string Currency,
    decimal TotalAmount,
    decimal AmountPaid,
    decimal OutstandingAmount,
    DateTime IssuedAtUtc,
    DateTime CreatedAtUtc);

public sealed record InvoiceDetails(
    Guid Id,
    string InvoiceNumber,
    Guid PatientId,
    string PatientName,
    Guid? VisitId,
    InvoiceStatus Status,
    string Currency,
    decimal TotalAmount,
    decimal AmountPaid,
    decimal OutstandingAmount,
    DateTime IssuedAtUtc,
    DateTime? VoidedAtUtc,
    string? VoidReason,
    Guid CreatedByUserId,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    IReadOnlyList<InvoiceLineResponse> Lines,
    IReadOnlyList<PaymentResponse> Payments);

public sealed record InvoiceLineRequest(string Description, decimal UnitPrice, int Quantity = 1);

public sealed record CreateInvoiceRequest(
    Guid PatientId,
    IReadOnlyList<InvoiceLineRequest> Lines,
    Guid? VisitId = null,
    string Currency = "EGP");

public sealed record RecordPaymentRequest(
    decimal Amount,
    PaymentMethod Method,
    string? Reference = null);

public sealed record VoidInvoiceRequest(string Reason);

public sealed record InvoiceSearchQuery
{
    public Guid? PatientId { get; init; }

    public Guid? VisitId { get; init; }

    public InvoiceStatus? Status { get; init; }

    public DateTime? FromUtc { get; init; }

    public DateTime? ToUtc { get; init; }

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = PageRequest.DefaultPageSize;
}
