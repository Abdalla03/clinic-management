using ClinicManagement.Application.Billing;
using ClinicManagement.Application.Billing.Validation;
using ClinicManagement.Application.Common.Paging;
using ClinicManagement.Domain.Billing;
using FluentValidation.TestHelper;
using Xunit;

namespace ClinicManagement.UnitTests.Billing;

public sealed class BillingValidatorTests
{
    private readonly CreateInvoiceRequestValidator _createValidator = new();
    private readonly RecordPaymentRequestValidator _paymentValidator = new();
    private readonly VoidInvoiceRequestValidator _voidValidator = new();
    private readonly InvoiceSearchQueryValidator _searchValidator = new();

    [Fact]
    public void Create_WithAWellFormedInvoice_IsValid()
    {
        var request = new CreateInvoiceRequest(
            Guid.NewGuid(),
            [new InvoiceLineRequest("Consultation", 150m, 1)],
            VisitId: Guid.NewGuid());

        var result = _createValidator.TestValidate(request);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Create_WithoutAPatient_Fails()
    {
        var request = new CreateInvoiceRequest(Guid.Empty, [new InvoiceLineRequest("Consultation", 150m)]);

        var result = _createValidator.TestValidate(request);

        Assert.Contains(result.Errors, failure => failure.PropertyName == nameof(CreateInvoiceRequest.PatientId));
    }

    [Fact]
    public void Create_WithoutLines_Fails()
    {
        var request = new CreateInvoiceRequest(Guid.NewGuid(), []);

        var result = _createValidator.TestValidate(request);

        Assert.Contains(result.Errors, failure => failure.PropertyName == nameof(CreateInvoiceRequest.Lines));
    }

    [Fact]
    public void Create_WithATwoLetterCurrency_Fails()
    {
        var request = new CreateInvoiceRequest(Guid.NewGuid(), [new InvoiceLineRequest("Consultation", 150m)], Currency: "E");

        var result = _createValidator.TestValidate(request);

        Assert.Contains(result.Errors, failure => failure.PropertyName == nameof(CreateInvoiceRequest.Currency));
    }

    [Fact]
    public void Create_WithAnInvalidLine_Fails()
    {
        var request = new CreateInvoiceRequest(
            Guid.NewGuid(),
            [new InvoiceLineRequest("", -1m, 0)]);

        var result = _createValidator.TestValidate(request);

        Assert.Contains(
            result.Errors,
            failure => failure.PropertyName.StartsWith($"{nameof(CreateInvoiceRequest.Lines)}[0]."));
    }

    [Fact]
    public void Create_WithTooManyLines_Fails()
    {
        var lines = Enumerable
            .Range(1, CreateInvoiceRequestValidator.MaxLines + 1)
            .Select(index => new InvoiceLineRequest($"Line {index}", 10m, 1))
            .ToArray();

        var result = _createValidator.TestValidate(new CreateInvoiceRequest(Guid.NewGuid(), lines));

        Assert.Contains(result.Errors, failure => failure.PropertyName == nameof(CreateInvoiceRequest.Lines));
    }

    [Fact]
    public void Payment_WithANonPositiveAmount_Fails()
    {
        var result = _paymentValidator.TestValidate(new RecordPaymentRequest(0m, PaymentMethod.Cash));

        Assert.Contains(result.Errors, failure => failure.PropertyName == nameof(RecordPaymentRequest.Amount));
    }

    [Fact]
    public void Payment_WithAnUnknownMethod_Fails()
    {
        var result = _paymentValidator.TestValidate(new RecordPaymentRequest(100m, (PaymentMethod)99));

        Assert.Contains(result.Errors, failure => failure.PropertyName == nameof(RecordPaymentRequest.Method));
    }

    [Fact]
    public void Payment_WithAWellFormedRequest_IsValid()
    {
        var result = _paymentValidator.TestValidate(
            new RecordPaymentRequest(120.5m, PaymentMethod.BankTransfer, "TRF-001"));

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Void_WithoutAReason_Fails()
    {
        var result = _voidValidator.TestValidate(new VoidInvoiceRequest(""));

        Assert.Contains(result.Errors, failure => failure.PropertyName == nameof(VoidInvoiceRequest.Reason));
    }

    [Fact]
    public void Void_WithAnOverlongReason_Fails()
    {
        var result = _voidValidator.TestValidate(new VoidInvoiceRequest(new string('x', 513)));

        Assert.Contains(result.Errors, failure => failure.PropertyName == nameof(VoidInvoiceRequest.Reason));
    }

    [Fact]
    public void Search_WithAnInvertedRange_Fails()
    {
        var query = new InvoiceSearchQuery
        {
            FromUtc = new DateTime(2026, 3, 14, 0, 0, 0, DateTimeKind.Utc),
            ToUtc = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc)
        };

        var result = _searchValidator.TestValidate(query);

        Assert.Contains(result.Errors, failure => failure.PropertyName == nameof(InvoiceSearchQuery.FromUtc));
    }

    [Fact]
    public void Search_WithAnOversizedPageSize_Fails()
    {
        var query = new InvoiceSearchQuery { PageSize = PageRequest.MaxPageSize + 1 };

        var result = _searchValidator.TestValidate(query);

        Assert.Contains(result.Errors, failure => failure.PropertyName == nameof(InvoiceSearchQuery.PageSize));
    }

    [Fact]
    public void Search_WithDefaults_IsValid()
    {
        var result = _searchValidator.TestValidate(new InvoiceSearchQuery());

        result.ShouldNotHaveAnyValidationErrors();
    }
}
