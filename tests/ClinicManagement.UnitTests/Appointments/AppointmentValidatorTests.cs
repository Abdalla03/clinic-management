using ClinicManagement.Application.Appointments;
using ClinicManagement.Application.Appointments.Validation;
using ClinicManagement.Application.Common.Paging;
using ClinicManagement.Domain.Appointments;
using FluentValidation.TestHelper;
using Xunit;

namespace ClinicManagement.UnitTests.Appointments;

public sealed class AppointmentValidatorTests
{
    private static readonly DateTime ValidStart = new(2026, 5, 4, 9, 0, 0, DateTimeKind.Utc);

    private readonly BookAppointmentRequestValidator _bookValidator = new();
    private readonly RescheduleAppointmentRequestValidator _rescheduleValidator = new();
    private readonly CancelAppointmentRequestValidator _cancelValidator = new();
    private readonly AppointmentSearchQueryValidator _searchValidator = new();
    private readonly UpsertDoctorScheduleRequestValidator _scheduleValidator = new();

    [Fact]
    public void Book_WithAWellFormedRequest_IsValid()
    {
        var request = new BookAppointmentRequest(
            Guid.NewGuid(),
            Guid.NewGuid(),
            ValidStart,
            DurationMinutes: 30,
            ReasonForVisit: "Annual check-up");

        var result = _bookValidator.TestValidate(request);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Book_WithoutIdentifiers_Fails()
    {
        var request = new BookAppointmentRequest(Guid.Empty, Guid.Empty, ValidStart);

        var result = _bookValidator.TestValidate(request);

        Assert.Contains(result.Errors, failure => failure.PropertyName == nameof(BookAppointmentRequest.PatientId));
        Assert.Contains(result.Errors, failure => failure.PropertyName == nameof(BookAppointmentRequest.DoctorId));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    [InlineData(481)]
    public void Book_WithADurationOutsideTheAllowedRange_Fails(int durationMinutes)
    {
        var request = new BookAppointmentRequest(Guid.NewGuid(), Guid.NewGuid(), ValidStart, durationMinutes);

        var result = _bookValidator.TestValidate(request);

        Assert.Contains(result.Errors, failure => failure.PropertyName == nameof(BookAppointmentRequest.DurationMinutes));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Book_WithAnEmptyStart_Fails(int seconds)
    {
        var request = new BookAppointmentRequest(
            Guid.NewGuid(),
            Guid.NewGuid(),
            DateTime.UnixEpoch.AddSeconds(seconds));

        var result = _bookValidator.TestValidate(request);

        Assert.Contains(result.Errors, failure => failure.PropertyName == nameof(BookAppointmentRequest.StartsAtUtc));
    }

    [Fact]
    public void Book_WithAnOverlongReason_Fails()
    {
        var request = new BookAppointmentRequest(
            Guid.NewGuid(),
            Guid.NewGuid(),
            ValidStart,
            ReasonForVisit: new string('x', 513));

        var result = _bookValidator.TestValidate(request);

        Assert.Contains(result.Errors, failure => failure.PropertyName == nameof(BookAppointmentRequest.ReasonForVisit));
    }

    [Fact]
    public void Reschedule_WithAWellFormedRequest_IsValid()
    {
        var request = new RescheduleAppointmentRequest(ValidStart.AddMinutes(60), 45);

        var result = _rescheduleValidator.TestValidate(request);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(481)]
    public void Reschedule_WithADurationOutsideTheAllowedRange_Fails(int durationMinutes)
    {
        var request = new RescheduleAppointmentRequest(ValidStart, durationMinutes);

        var result = _rescheduleValidator.TestValidate(request);

        Assert.Contains(result.Errors, failure => failure.PropertyName == nameof(RescheduleAppointmentRequest.DurationMinutes));
    }

    [Fact]
    public void Cancel_WithoutAReason_IsValid()
    {
        var result = _cancelValidator.TestValidate(new CancelAppointmentRequest());

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Cancel_WithAnOverlongReason_Fails()
    {
        var result = _cancelValidator.TestValidate(new CancelAppointmentRequest(new string('x', 513)));

        Assert.Contains(result.Errors, failure => failure.PropertyName == nameof(CancelAppointmentRequest.Reason));
    }

    [Fact]
    public void Search_WithTheDefaults_IsValid()
    {
        var result = _searchValidator.TestValidate(new AppointmentSearchQuery());

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Search_WithAnInvertedRange_Fails()
    {
        var query = new AppointmentSearchQuery
        {
            FromUtc = ValidStart.AddDays(7),
            ToUtc = ValidStart
        };

        var result = _searchValidator.TestValidate(query);

        Assert.Contains(result.Errors, failure => failure.PropertyName == string.Empty);
    }

    [Fact]
    public void Search_WithARangeLongerThanTheAllowedWindow_Fails()
    {
        var query = new AppointmentSearchQuery
        {
            FromUtc = ValidStart,
            ToUtc = ValidStart.AddDays(AppointmentSearchQueryValidator.MaxRangeDays + 1)
        };

        var result = _searchValidator.TestValidate(query);

        Assert.Contains(result.Errors, failure => failure.PropertyName == string.Empty);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public void Search_WithAPageSizeOutsideTheAllowedRange_Fails(int pageSize)
    {
        var query = new AppointmentSearchQuery { PageSize = pageSize };

        var result = _searchValidator.TestValidate(query);

        Assert.Contains(result.Errors, failure => failure.PropertyName == nameof(AppointmentSearchQuery.PageSize));
    }

    [Fact]
    public void Search_WithAPageBelowOne_Fails()
    {
        var query = new AppointmentSearchQuery { Page = 0 };

        var result = _searchValidator.TestValidate(query);

        Assert.Contains(result.Errors, failure => failure.PropertyName == nameof(AppointmentSearchQuery.Page));
    }

    [Fact]
    public void Search_WithAMaximumSizedPage_IsValid()
    {
        var query = new AppointmentSearchQuery { Page = 1, PageSize = PageRequest.MaxPageSize };

        var result = _searchValidator.TestValidate(query);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Schedule_WithAWellFormedWindow_IsValid()
    {
        var request = new UpsertDoctorScheduleRequest(
            DayOfWeek.Saturday,
            new TimeOnly(9, 0),
            new TimeOnly(17, 0),
            SlotMinutes: 20);

        var result = _scheduleValidator.TestValidate(request);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Schedule_WithAnEndBeforeItsStart_Fails()
    {
        var request = new UpsertDoctorScheduleRequest(
            DayOfWeek.Saturday,
            new TimeOnly(17, 0),
            new TimeOnly(9, 0));

        var result = _scheduleValidator.TestValidate(request);

        Assert.Contains(result.Errors, failure => failure.PropertyName == nameof(UpsertDoctorScheduleRequest.EndsAt));
    }

    [Fact]
    public void Schedule_WithAnEndEqualToItsStart_Fails()
    {
        var request = new UpsertDoctorScheduleRequest(
            DayOfWeek.Saturday,
            new TimeOnly(9, 0),
            new TimeOnly(9, 0));

        var result = _scheduleValidator.TestValidate(request);

        Assert.Contains(result.Errors, failure => failure.PropertyName == nameof(UpsertDoctorScheduleRequest.EndsAt));
    }

    [Theory]
    [InlineData(4)]
    [InlineData(481)]
    public void Schedule_WithASlotLengthOutsideTheAllowedRange_Fails(int slotMinutes)
    {
        var request = new UpsertDoctorScheduleRequest(
            DayOfWeek.Saturday,
            new TimeOnly(9, 0),
            new TimeOnly(17, 0),
            slotMinutes);

        var result = _scheduleValidator.TestValidate(request);

        Assert.Contains(result.Errors, failure => failure.PropertyName == nameof(UpsertDoctorScheduleRequest.SlotMinutes));
    }

    [Theory]
    [InlineData((int)DayOfWeek.Sunday)]
    [InlineData((int)DayOfWeek.Saturday)]
    public void Schedule_WithADefinedDayOfWeek_IsValid(int dayOfWeek)
    {
        var request = new UpsertDoctorScheduleRequest(
            (DayOfWeek)dayOfWeek,
            new TimeOnly(9, 0),
            new TimeOnly(17, 0));

        var result = _scheduleValidator.TestValidate(request);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Schedule_WithADayOfWeekOutsideTheEnum_Fails()
    {
        var request = new UpsertDoctorScheduleRequest(
            (DayOfWeek)7,
            new TimeOnly(9, 0),
            new TimeOnly(17, 0));

        var result = _scheduleValidator.TestValidate(request);

        Assert.Contains(result.Errors, failure => failure.PropertyName == nameof(UpsertDoctorScheduleRequest.DayOfWeek));
    }
}
