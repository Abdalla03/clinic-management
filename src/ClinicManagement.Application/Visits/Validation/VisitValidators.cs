using ClinicManagement.Application.Common.Paging;
using ClinicManagement.Application.Visits;
using FluentValidation;

namespace ClinicManagement.Application.Visits.Validation;

public sealed class RecordVisitRequestValidator : AbstractValidator<RecordVisitRequest>
{
    public RecordVisitRequestValidator()
    {
        RuleFor(request => request.PatientId).NotEmpty();
        RuleFor(request => request.DoctorId).NotEmpty();
        RuleFor(request => request.VisitAtUtc)
            .NotEmpty()
            .Must(visitAt => visitAt.Year > 2000)
            .WithMessage("visitAtUtc must be an ISO-8601 timestamp.");
        RuleFor(request => request.ChiefComplaint).MaximumLength(2048)
            .When(request => request.ChiefComplaint is not null);
        RuleFor(request => request.Notes).MaximumLength(8192)
            .When(request => request.Notes is not null);
        RuleFor(request => request.Diagnosis).MaximumLength(2048)
            .When(request => request.Diagnosis is not null);
    }
}

public sealed class UpdateVisitRequestValidator : AbstractValidator<UpdateVisitRequest>
{
    public UpdateVisitRequestValidator()
    {
        RuleFor(request => request.ChiefComplaint).MaximumLength(2048)
            .When(request => request.ChiefComplaint is not null);
        RuleFor(request => request.Notes).MaximumLength(8192)
            .When(request => request.Notes is not null);
        RuleFor(request => request.Diagnosis).MaximumLength(2048)
            .When(request => request.Diagnosis is not null);
    }
}

public sealed class VisitSearchQueryValidator : AbstractValidator<VisitSearchQuery>
{
    public VisitSearchQueryValidator()
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
