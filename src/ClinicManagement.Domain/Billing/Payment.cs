using ClinicManagement.Domain.Common;

namespace ClinicManagement.Domain.Billing;

public sealed class Payment
{
    private Payment()
    {
    }

    public Guid Id { get; private set; }

    public Guid InvoiceId { get; private set; }

    public Invoice? Invoice { get; private set; }

    public decimal Amount { get; private set; }

    public DateTime PaidAtUtc { get; private set; }

    public PaymentMethod Method { get; private set; }

    public string? Reference { get; private set; }

    public Guid ReceivedByUserId { get; private set; }

    public static Payment Record(
        Guid invoiceId,
        decimal amount,
        PaymentMethod method,
        string? reference,
        Guid receivedByUserId,
        DateTime nowUtc)
    {
        if (invoiceId == Guid.Empty)
        {
            throw new DomainException(ErrorCodes.ValidationFailed, "invoiceId is required.");
        }

        if (amount <= 0)
        {
            throw new DomainException(ErrorCodes.ValidationFailed, "amount must be greater than zero.");
        }

        if (!Enum.IsDefined(method))
        {
            throw new DomainException(ErrorCodes.ValidationFailed, "method is invalid.");
        }

        return new Payment
        {
            Id = Guid.NewGuid(),
            InvoiceId = invoiceId,
            Amount = Math.Round(amount, 2, MidpointRounding.AwayFromZero),
            PaidAtUtc = nowUtc,
            Method = method,
            Reference = string.IsNullOrWhiteSpace(reference) ? null : reference.Trim(),
            ReceivedByUserId = receivedByUserId
        };
    }
}
