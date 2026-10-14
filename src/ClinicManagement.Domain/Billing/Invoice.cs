using ClinicManagement.Domain.Common;

namespace ClinicManagement.Domain.Billing;

public enum InvoiceStatus
{
    Draft = 1,
    Issued = 2,
    PartiallyPaid = 3,
    Paid = 4,
    Void = 5
}

public enum PaymentMethod
{
    Cash = 1,
    Card = 2,
    BankTransfer = 3,
    Insurance = 4
}

public sealed class Invoice
{
    private readonly List<InvoiceLine> _lines = [];
    private readonly List<Payment> _payments = [];

    private Invoice()
    {
    }

    public Guid Id { get; private set; }

    public string InvoiceNumber { get; private set; } = string.Empty;

    public Guid PatientId { get; private set; }

    public Patients.Patient? Patient { get; private set; }

    public Guid? VisitId { get; private set; }

    public Patients.PatientVisit? Visit { get; private set; }

    public InvoiceStatus Status { get; private set; }

    public string Currency { get; private set; } = string.Empty;

    public decimal TotalAmount { get; private set; }

    public decimal AmountPaid { get; private set; }

    public decimal OutstandingAmount => TotalAmount - AmountPaid;

    public DateTime IssuedAtUtc { get; private set; }

    public DateTime? VoidedAtUtc { get; private set; }

    public string? VoidReason { get; private set; }

    public Guid CreatedByUserId { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

    public IReadOnlyCollection<InvoiceLine> Lines => _lines;

    public IReadOnlyCollection<Payment> Payments => _payments;

    public static Invoice Create(
        string invoiceNumber,
        Guid patientId,
        Guid? visitId,
        string currency,
        IEnumerable<InvoiceLineInput> lines,
        Guid createdByUserId,
        DateTime nowUtc)
    {
        if (string.IsNullOrWhiteSpace(invoiceNumber))
        {
            throw new DomainException(ErrorCodes.ValidationFailed, "invoiceNumber is required.");
        }

        if (patientId == Guid.Empty)
        {
            throw new DomainException(ErrorCodes.ValidationFailed, "patientId is required.");
        }

        if (string.IsNullOrWhiteSpace(currency))
        {
            throw new DomainException(ErrorCodes.ValidationFailed, "currency is required.");
        }

        var lineInputs = (lines ?? []).ToList();
        if (lineInputs.Count == 0)
        {
            throw new DomainException(ErrorCodes.ValidationFailed, "An invoice needs at least one line.");
        }

        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            InvoiceNumber = invoiceNumber.Trim().ToUpperInvariant(),
            PatientId = patientId,
            VisitId = visitId,
            Currency = currency.Trim().ToUpperInvariant(),
            Status = InvoiceStatus.Draft,
            CreatedByUserId = createdByUserId,
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc
        };

        foreach (var input in lineInputs)
        {
            invoice.AddLineInternal(input.Description, input.UnitPrice, input.Quantity);
        }

        invoice.TotalAmount = invoice._lines.Sum(line => line.LineTotal);
        return invoice;
    }

    public void Issue(DateTime nowUtc)
    {
        if (Status != InvoiceStatus.Draft)
        {
            throw new DomainException(
                ErrorCodes.InvalidStateTransition,
                $"An invoice in the '{Status}' state cannot be issued.");
        }

        Status = InvoiceStatus.Issued;
        IssuedAtUtc = nowUtc;
        Touch(nowUtc);
    }

    public void RecordPayment(
        decimal amount,
        PaymentMethod method,
        string? reference,
        Guid receivedByUserId,
        DateTime nowUtc)
    {
        if (Status is InvoiceStatus.Draft)
        {
            throw new DomainException(
                ErrorCodes.InvalidStateTransition,
                "Payments can only be recorded against an issued invoice.");
        }

        if (Status is InvoiceStatus.Void)
        {
            throw new DomainException(
                ErrorCodes.InvalidStateTransition,
                "A void invoice cannot receive payments.");
        }

        if (Status is InvoiceStatus.Paid)
        {
            throw new DomainException(
                ErrorCodes.InvalidStateTransition,
                "The invoice is already fully paid.");
        }

        if (amount <= 0)
        {
            throw new DomainException(ErrorCodes.ValidationFailed, "amount must be greater than zero.");
        }

        if (amount > OutstandingAmount)
        {
            throw new DomainException(ErrorCodes.ValidationFailed, "amount exceeds the outstanding balance.");
        }

        _payments.Add(Payment.Record(Id, amount, method, reference, receivedByUserId, nowUtc));
        AmountPaid = Math.Round(AmountPaid + amount, 2, MidpointRounding.AwayFromZero);
        Status = AmountPaid >= TotalAmount ? InvoiceStatus.Paid : InvoiceStatus.PartiallyPaid;
        Touch(nowUtc);
    }

    public void Void(string reason, DateTime nowUtc)
    {
        if (Status is InvoiceStatus.Void)
        {
            throw new DomainException(ErrorCodes.InvalidStateTransition, "The invoice is already void.");
        }

        if (Status is InvoiceStatus.Paid)
        {
            throw new DomainException(
                ErrorCodes.InvalidStateTransition,
                "A paid invoice cannot be voided. Record a refund instead.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new DomainException(ErrorCodes.ValidationFailed, "A void reason is required.");
        }

        Status = InvoiceStatus.Void;
        VoidedAtUtc = nowUtc;
        VoidReason = reason.Trim();
        Touch(nowUtc);
    }

    private void AddLineInternal(string description, decimal unitPrice, int quantity)
    {
        _lines.Add(InvoiceLine.Create(Id, description, unitPrice, quantity));
    }

    private void Touch(DateTime nowUtc)
    {
        UpdatedAtUtc = nowUtc;
    }
}

public readonly record struct InvoiceLineInput(string Description, decimal UnitPrice, int Quantity);
