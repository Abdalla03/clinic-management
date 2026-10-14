using ClinicManagement.Application.Billing;
using ClinicManagement.Application.Common.Paging;
using FluentValidation;

namespace ClinicManagement.Application.Billing.Validation;

public sealed class CreateInvoiceRequestValidator : AbstractValidator<CreateInvoiceRequest>
{
    public const int MaxLines = 100;

    public CreateInvoiceRequestValidator()
    {
        RuleFor(request => request.PatientId).NotEmpty();

        RuleFor(request => request.Currency)
            .NotEmpty()
            .Length(3)
            .WithMessage("currency must be a three-letter ISO-4217 code.");

        RuleForEach(request => request.Lines)
            .SetValidator(new InvoiceLineRequestValidator())
            .When(request => request.Lines is not null);

        RuleFor(request => request.Lines)
            .NotEmpty()
            .WithMessage("An invoice needs at least one line.");

        RuleFor(request => request.Lines)
            .Must(lines => lines is null || lines.Count <= MaxLines)
            .WithMessage($"An invoice may contain at most {MaxLines} lines.");
    }
}

public sealed class InvoiceLineRequestValidator : AbstractValidator<InvoiceLineRequest>
{
    public InvoiceLineRequestValidator()
    {
        RuleFor(line => line.Description)
            .NotEmpty()
            .MaximumLength(512);

        RuleFor(line => line.UnitPrice)
            .InclusiveBetween(0, 1_000_000)
            .WithMessage("unitPrice must be between 0 and 1000000.");

        RuleFor(line => line.Quantity)
            .InclusiveBetween(1, 1000)
            .WithMessage("quantity must be between 1 and 1000.");
    }
}

public sealed class RecordPaymentRequestValidator : AbstractValidator<RecordPaymentRequest>
{
    public RecordPaymentRequestValidator()
    {
        RuleFor(request => request.Amount)
            .GreaterThan(0)
            .WithMessage("amount must be greater than zero.");

        RuleFor(request => request.Method)
            .IsInEnum()
            .WithMessage("method is invalid.");

        RuleFor(request => request.Reference)
            .MaximumLength(128)
            .When(request => request.Reference is not null);
    }
}

public sealed class VoidInvoiceRequestValidator : AbstractValidator<VoidInvoiceRequest>
{
    public VoidInvoiceRequestValidator()
    {
        RuleFor(request => request.Reason)
            .NotEmpty()
            .MaximumLength(512);
    }
}

public sealed class InvoiceSearchQueryValidator : AbstractValidator<InvoiceSearchQuery>
{
    public InvoiceSearchQueryValidator()
    {
        RuleFor(query => query.Page)
            .InclusiveBetween(1, 100_000)
            .WithMessage("page must be between 1 and 100000.");

        RuleFor(query => query.PageSize)
            .InclusiveBetween(1, PageRequest.MaxPageSize)
            .WithMessage($"pageSize must be between 1 and {PageRequest.MaxPageSize}.");

        RuleFor(query => query.FromUtc)
            .Must((query, fromUtc) => fromUtc is null || query.ToUtc is null || fromUtc <= query.ToUtc)
            .WithMessage("fromUtc must be before toUtc.");
    }
}
