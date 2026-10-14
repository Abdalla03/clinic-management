using ClinicManagement.Application.Common.Paging;
using ClinicManagement.Application.Visits;
using ClinicManagement.Application.Visits.Validation;
using FluentValidation.TestHelper;
using Xunit;

namespace ClinicManagement.UnitTests.Visits;

public sealed class VisitValidatorTests
{
    private static readonly DateTime ValidVisitAt = new(2026, 5, 4, 10, 0, 0, DateTimeKind.Utc);

    private readonly RecordVisitRequestValidator _recordValidator = new();
    private readonly UpdateVisitRequestValidator _updateValidator = new();
    private readonly VisitSearchQueryValidator _searchValidator = new();

    [Fact]
    public void Record_WithAWellFormedVisit_IsValid()
    {
        var request = new RecordVisitRequest(
            Guid.NewGuid(),
            Guid.NewGuid(),
            ValidVisitAt,
            AppointmentId: Guid.NewGuid(),
            ChiefComplaint: "Headache for three days",
            Notes: "Advised rest and hydration.",
            Diagnosis: "Tension headache");

        var result = _recordValidator.TestValidate(request);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Record_WithoutIdentifiers_Fails()
    {
        var request = new RecordVisitRequest(Guid.Empty, Guid.Empty, ValidVisitAt);

        var result = _recordValidator.TestValidate(request);

        Assert.Contains(result.Errors, failure => failure.PropertyName == nameof(RecordVisitRequest.PatientId));
        Assert.Contains(result.Errors, failure => failure.PropertyName == nameof(RecordVisitRequest.DoctorId));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Record_WithAnEmptyVisitTimestamp_Fails(int seconds)
    {
        var request = new RecordVisitRequest(
            Guid.NewGuid(),
            Guid.NewGuid(),
            DateTime.UnixEpoch.AddSeconds(seconds));

        var result = _recordValidator.TestValidate(request);

        Assert.Contains(result.Errors, failure => failure.PropertyName == nameof(RecordVisitRequest.VisitAtUtc));
    }

    [Theory]
    [InlineData("ChiefComplaint")]
    [InlineData("Notes")]
    [InlineData("Diagnosis")]
    public void Record_WithOverlongClinicalText_Fails(string propertyName)
    {
        var request = new RecordVisitRequest(
            Guid.NewGuid(),
            Guid.NewGuid(),
            ValidVisitAt,
            ChiefComplaint: new string('a', 2049),
            Notes: new string('b', 8193),
            Diagnosis: new string('c', 2049));

        var result = _recordValidator.TestValidate(request);

        Assert.Contains(result.Errors, failure => failure.PropertyName == propertyName);
    }

    [Fact]
    public void Update_WithoutAnyContent_IsValid()
    {
        var result = _updateValidator.TestValidate(new UpdateVisitRequest());

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Update_WithAnOverlongDiagnosis_Fails()
    {
        var result = _updateValidator.TestValidate(new UpdateVisitRequest(Diagnosis: new string('x', 2049)));

        Assert.Contains(result.Errors, failure => failure.PropertyName == nameof(UpdateVisitRequest.Diagnosis));
    }

    [Fact]
    public void Search_WithTheDefaults_IsValid()
    {
        var result = _searchValidator.TestValidate(new VisitSearchQuery());

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Search_WithAnInvertedRange_Fails()
    {
        var query = new VisitSearchQuery
        {
            FromUtc = ValidVisitAt.AddDays(7),
            ToUtc = ValidVisitAt
        };

        var result = _searchValidator.TestValidate(query);

        Assert.Contains(result.Errors, failure => failure.PropertyName == string.Empty);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public void Search_WithAPageSizeOutsideTheAllowedRange_Fails(int pageSize)
    {
        var query = new VisitSearchQuery { PageSize = pageSize };

        var result = _searchValidator.TestValidate(query);

        Assert.Contains(result.Errors, failure => failure.PropertyName == nameof(VisitSearchQuery.PageSize));
    }

    [Fact]
    public void Search_WithAMaximumSizedPage_IsValid()
    {
        var query = new VisitSearchQuery { Page = 3, PageSize = PageRequest.MaxPageSize };

        var result = _searchValidator.TestValidate(query);

        result.ShouldNotHaveAnyValidationErrors();
    }
}
