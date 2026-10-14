using ClinicManagement.Application.Common.Paging;
using ClinicManagement.Application.Prescriptions;
using FluentValidation;

namespace ClinicManagement.Application.Prescriptions.Validation;

public sealed class IssuePrescriptionRequestValidator : AbstractValidator<IssuePrescriptionRequest>
{
    public const int MaxItems = 50;

    public IssuePrescriptionRequestValidator()
    {
        RuleFor(request => request.PatientId).NotEmpty();

        RuleFor(request => request.Notes)
            .MaximumLength(2048)
            .When(request => request.Notes is not null);

        RuleForEach(request => request.Items)
            .SetValidator(new PrescriptionItemRequestValidator())
            .When(request => request.Items is not null);

        RuleFor(request => request.Items)
            .NotEmpty()
            .WithMessage("At least one prescription item is required.");

        RuleFor(request => request.Items)
            .Must(items => items is null || items.Count <= MaxItems)
            .WithMessage($"A prescription may contain at most {MaxItems} items.");
    }
}

public sealed class PrescriptionItemRequestValidator : AbstractValidator<PrescriptionItemRequest>
{
    public PrescriptionItemRequestValidator()
    {
        RuleFor(item => item.DrugName)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(item => item.Dose)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(item => item.Route)
            .MaximumLength(100)
            .When(item => item.Route is not null);

        RuleFor(item => item.Instructions)
            .MaximumLength(1024)
            .When(item => item.Instructions is not null);

        RuleFor(item => item.TimesPerDay)
            .InclusiveBetween(1, 12)
            .WithMessage("timesPerDay must be between 1 and 12.");

        RuleFor(item => item.DurationDays)
            .InclusiveBetween(1, 365)
            .WithMessage("durationDays must be between 1 and 365.");

        RuleFor(item => item)
            .Must(item => item.DoseTimes is null || item.DoseTimes.Count == item.TimesPerDay)
            .WithMessage("doseTimes count must equal timesPerDay when dose times are supplied.");
    }
}

public sealed class PrescriptionSearchQueryValidator : AbstractValidator<PrescriptionSearchQuery>
{
    public PrescriptionSearchQueryValidator()
    {
        RuleFor(query => query.Page)
            .InclusiveBetween(1, 100_000)
            .WithMessage("page must be between 1 and 100000.");

        RuleFor(query => query.PageSize)
            .InclusiveBetween(1, PageRequest.MaxPageSize)
            .WithMessage($"pageSize must be between 1 and {PageRequest.MaxPageSize}.");

        RuleFor(query => query)
            .Must(query => query.FromUtc is null || query.ToUtc is null || query.FromUtc <= query.ToUtc)
            .WithMessage("fromUtc must be before toUtc.");
    }
}
