using ClinicManagement.Application.Appointments;
using ClinicManagement.Application.Common.Paging;
using FluentValidation;

namespace ClinicManagement.Application.Appointments.Validation;

public sealed class BookAppointmentRequestValidator : AbstractValidator<BookAppointmentRequest>
{
    public const int MinDurationMinutes = 5;

    public const int MaxDurationMinutes = 480;

    public BookAppointmentRequestValidator()
    {
        RuleFor(request => request.PatientId).NotEmpty();
        RuleFor(request => request.DoctorId).NotEmpty();
        RuleFor(request => request.StartsAtUtc)
            .NotEmpty()
            .Must(startsAt => startsAt.Year > 2000)
            .WithMessage("startsAtUtc must be an ISO-8601 timestamp.");
        RuleFor(request => request.DurationMinutes)
            .InclusiveBetween(MinDurationMinutes, MaxDurationMinutes);
        RuleFor(request => request.ReasonForVisit)
            .MaximumLength(512)
            .When(request => request.ReasonForVisit is not null);
    }
}

public sealed class RescheduleAppointmentRequestValidator : AbstractValidator<RescheduleAppointmentRequest>
{
    public RescheduleAppointmentRequestValidator()
    {
        RuleFor(request => request.StartsAtUtc)
            .NotEmpty()
            .Must(startsAt => startsAt.Year > 2000)
            .WithMessage("startsAtUtc must be an ISO-8601 timestamp.");
        RuleFor(request => request.DurationMinutes)
            .InclusiveBetween(
                BookAppointmentRequestValidator.MinDurationMinutes,
                BookAppointmentRequestValidator.MaxDurationMinutes);
    }
}

public sealed class CancelAppointmentRequestValidator : AbstractValidator<CancelAppointmentRequest>
{
    public CancelAppointmentRequestValidator()
    {
        RuleFor(request => request.Reason)
            .MaximumLength(512)
            .When(request => request.Reason is not null);
    }
}

public sealed class AppointmentSearchQueryValidator : AbstractValidator<AppointmentSearchQuery>
{
    public const int MaxRangeDays = 366;

    public AppointmentSearchQueryValidator()
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

        RuleFor(query => query)
            .Must(query => query.FromUtc is null || query.ToUtc is null ||
                           (query.ToUtc.Value - query.FromUtc.Value).TotalDays <= MaxRangeDays)
            .WithMessage($"The requested range may not exceed {MaxRangeDays} days.");
    }
}

public sealed class UpsertDoctorScheduleRequestValidator : AbstractValidator<UpsertDoctorScheduleRequest>
{
    public UpsertDoctorScheduleRequestValidator()
    {
        RuleFor(request => request.DayOfWeek)
            .IsInEnum()
            .WithMessage("dayOfWeek must be between 0 (Sunday) and 6 (Saturday).");

        RuleFor(request => request.EndsAt)
            .GreaterThan(request => request.StartsAt)
            .WithMessage("endsAt must be after startsAt.");

        RuleFor(request => request.SlotMinutes)
            .InclusiveBetween(5, 480);
    }
}
