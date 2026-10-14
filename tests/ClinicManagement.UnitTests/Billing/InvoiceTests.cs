using ClinicManagement.Domain.Billing;
using ClinicManagement.Domain.Common;
using Xunit;

namespace ClinicManagement.UnitTests.Billing;

public sealed class InvoiceTests
{
    private static readonly Guid PatientId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid CreatedBy = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly DateTime Now = new(2026, 3, 14, 8, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Create_SumsLineTotalsAndStartsAsDraft()
    {
        var invoice = Create(
        [
            new InvoiceLineInput("Consultation", 150m, 1),
            new InvoiceLineInput("Lab work", 40.5m, 2)
        ]);

        Assert.Equal(InvoiceStatus.Draft, invoice.Status);
        Assert.Equal(231.0m, invoice.TotalAmount);
        Assert.Equal(0m, invoice.AmountPaid);
        Assert.Equal(231.0m, invoice.OutstandingAmount);
        Assert.Equal(2, invoice.Lines.Count);
        Assert.Equal("INV-20260314080000-ABCD1234", invoice.InvoiceNumber);
        Assert.Equal("EGP", invoice.Currency);
    }

    [Fact]
    public void Create_WithNoLines_Throws()
    {
        var exception = Assert.Throws<DomainException>(() => Create([]));

        Assert.Equal(ErrorCodes.ValidationFailed, exception.Code);
    }

    [Fact]
    public void Create_WithAnEmptyPatient_Throws()
    {
        var exception = Assert.Throws<DomainException>(() =>
            Invoice.Create("INV-1", Guid.Empty, null, "EGP", [new InvoiceLineInput("Visit", 100m, 1)], CreatedBy, Now));

        Assert.Equal(ErrorCodes.ValidationFailed, exception.Code);
    }

    [Fact]
    public void Issue_MovesDraftToIssued()
    {
        var invoice = Create();

        invoice.Issue(Now);

        Assert.Equal(InvoiceStatus.Issued, invoice.Status);
        Assert.Equal(Now, invoice.IssuedAtUtc);
    }

    [Fact]
    public void Issue_WhenAlreadyIssued_Throws()
    {
        var invoice = Create();
        invoice.Issue(Now);

        var exception = Assert.Throws<DomainException>(() => invoice.Issue(Now.AddMinutes(1)));

        Assert.Equal(ErrorCodes.InvalidStateTransition, exception.Code);
    }

    [Fact]
    public void RecordPayment_OnADraftInvoice_Throws()
    {
        var invoice = Create();

        var exception = Assert.Throws<DomainException>(() =>
            invoice.RecordPayment(100m, PaymentMethod.Cash, null, CreatedBy, Now));

        Assert.Equal(ErrorCodes.InvalidStateTransition, exception.Code);
    }

    [Fact]
    public void RecordPayment_PartialPayment_LeavesTheInvoicePartiallyPaid()
    {
        var invoice = Create([new InvoiceLineInput("Consultation", 100m, 1)]);
        invoice.Issue(Now);

        invoice.RecordPayment(40m, PaymentMethod.Cash, null, CreatedBy, Now);

        Assert.Equal(InvoiceStatus.PartiallyPaid, invoice.Status);
        Assert.Equal(40m, invoice.AmountPaid);
        Assert.Equal(60m, invoice.OutstandingAmount);
        Assert.Single(invoice.Payments);
    }

    [Fact]
    public void RecordPayment_PayingTheFullBalance_MarksTheInvoicePaid()
    {
        var invoice = Create([new InvoiceLineInput("Consultation", 100m, 1)]);
        invoice.Issue(Now);

        invoice.RecordPayment(100m, PaymentMethod.Card, "POS-42", CreatedBy, Now);

        Assert.Equal(InvoiceStatus.Paid, invoice.Status);
        Assert.Equal(100m, invoice.AmountPaid);
        Assert.Equal(0m, invoice.OutstandingAmount);
    }

    [Fact]
    public void RecordPayment_ExceedingTheOutstandingBalance_Throws()
    {
        var invoice = Create([new InvoiceLineInput("Consultation", 100m, 1)]);
        invoice.Issue(Now);

        var exception = Assert.Throws<DomainException>(() =>
            invoice.RecordPayment(150m, PaymentMethod.Cash, null, CreatedBy, Now));

        Assert.Equal(ErrorCodes.ValidationFailed, exception.Code);
    }

    [Fact]
    public void RecordPayment_OnAPaidInvoice_Throws()
    {
        var invoice = Create([new InvoiceLineInput("Consultation", 100m, 1)]);
        invoice.Issue(Now);
        invoice.RecordPayment(100m, PaymentMethod.Cash, null, CreatedBy, Now);

        var exception = Assert.Throws<DomainException>(() =>
            invoice.RecordPayment(1m, PaymentMethod.Cash, null, CreatedBy, Now));

        Assert.Equal(ErrorCodes.InvalidStateTransition, exception.Code);
    }

    [Fact]
    public void RecordPayment_WithANonPositiveAmount_Throws()
    {
        var invoice = Create();
        invoice.Issue(Now);

        var exception = Assert.Throws<DomainException>(() =>
            invoice.RecordPayment(0m, PaymentMethod.Cash, null, CreatedBy, Now));

        Assert.Equal(ErrorCodes.ValidationFailed, exception.Code);
    }

    [Fact]
    public void Void_MovesAnUnpaidInvoiceToVoid()
    {
        var invoice = Create();
        invoice.Issue(Now);

        invoice.Void("duplicate invoice", Now);

        Assert.Equal(InvoiceStatus.Void, invoice.Status);
        Assert.Equal(Now, invoice.VoidedAtUtc);
        Assert.Equal("duplicate invoice", invoice.VoidReason);
    }

    [Fact]
    public void Void_APaidInvoice_Throws()
    {
        var invoice = Create([new InvoiceLineInput("Consultation", 100m, 1)]);
        invoice.Issue(Now);
        invoice.RecordPayment(100m, PaymentMethod.Cash, null, CreatedBy, Now);

        var exception = Assert.Throws<DomainException>(() => invoice.Void("changed my mind", Now));

        Assert.Equal(ErrorCodes.InvalidStateTransition, exception.Code);
    }

    [Fact]
    public void Void_WithoutAReason_Throws()
    {
        var invoice = Create();
        invoice.Issue(Now);

        var exception = Assert.Throws<DomainException>(() => invoice.Void("  ", Now));

        Assert.Equal(ErrorCodes.ValidationFailed, exception.Code);
    }

    [Fact]
    public void Void_WhenAlreadyVoid_Throws()
    {
        var invoice = Create();
        invoice.Issue(Now);
        invoice.Void("duplicate", Now);

        var exception = Assert.Throws<DomainException>(() => invoice.Void("again", Now));

        Assert.Equal(ErrorCodes.InvalidStateTransition, exception.Code);
    }

    private static Invoice Create(IReadOnlyList<InvoiceLineInput>? lines = null) =>
        Invoice.Create(
            "inv-20260314080000-abcd1234",
            PatientId,
            null,
            "egp",
            lines ?? [new InvoiceLineInput("Consultation", 150m, 1)],
            CreatedBy,
            Now);
}
