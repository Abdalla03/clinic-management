using ClinicManagement.Domain.Common;

namespace ClinicManagement.Domain.Billing;

public sealed class InvoiceLine
{
    private InvoiceLine()
    {
    }

    public Guid Id { get; private set; }

    public Guid InvoiceId { get; private set; }

    public Invoice? Invoice { get; private set; }

    public string Description { get; private set; } = string.Empty;

    public decimal UnitPrice { get; private set; }

    public int Quantity { get; private set; }

    public decimal LineTotal => Math.Round(UnitPrice * Quantity, 2, MidpointRounding.AwayFromZero);

    public static InvoiceLine Create(Guid invoiceId, string description, decimal unitPrice, int quantity)
    {
        if (invoiceId == Guid.Empty)
        {
            throw new DomainException(ErrorCodes.ValidationFailed, "invoiceId is required.");
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            throw new DomainException(ErrorCodes.ValidationFailed, "description is required.");
        }

        if (unitPrice < 0)
        {
            throw new DomainException(ErrorCodes.ValidationFailed, "unitPrice cannot be negative.");
        }

        if (quantity is < 1 or > 1000)
        {
            throw new DomainException(ErrorCodes.ValidationFailed, "quantity must be between 1 and 1000.");
        }

        return new InvoiceLine
        {
            Id = Guid.NewGuid(),
            InvoiceId = invoiceId,
            Description = description.Trim(),
            UnitPrice = Math.Round(unitPrice, 2, MidpointRounding.AwayFromZero),
            Quantity = quantity
        };
    }
}
