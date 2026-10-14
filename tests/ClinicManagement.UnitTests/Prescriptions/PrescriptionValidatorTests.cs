using ClinicManagement.Application.Common.Paging;
using ClinicManagement.Application.Prescriptions;
using ClinicManagement.Application.Prescriptions.Validation;
using FluentValidation.TestHelper;
using Xunit;

namespace ClinicManagement.UnitTests.Prescriptions;

public sealed class PrescriptionValidatorTests
{
    private readonly IssuePrescriptionRequestValidator _issueValidator = new();
    private readonly PrescriptionItemRequestValidator _itemValidator = new();
    private readonly PrescriptionSearchQueryValidator _searchValidator = new();

    [Fact]
    public void Issue_WithAWellFormedPrescription_IsValid()
    {
        var request = new IssuePrescriptionRequest(
            Guid.NewGuid(),
            Items:
            [
                new PrescriptionItemRequest(
                    "Amoxicillin",
                    "500mg",
                    TimesPerDay: 3,
                    DurationDays: 7,
                    Route: "Oral",
                    Instructions: "Take after meals.",
                    DoseTimes: [new TimeOnly(8, 0), new TimeOnly(14, 0), new TimeOnly(20, 0)])
            ],
            VisitId: Guid.NewGuid(),
            Notes: "Follow up in one week.");

        var result = _issueValidator.TestValidate(request);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Issue_WithoutAPatient_Fails()
    {
        var request = new IssuePrescriptionRequest(
            Guid.Empty,
            Items: [new PrescriptionItemRequest("Amoxicillin", "500mg", TimesPerDay: 1, DurationDays: 7)]);

        var result = _issueValidator.TestValidate(request);

        Assert.Contains(result.Errors, failure => failure.PropertyName == nameof(IssuePrescriptionRequest.PatientId));
    }

    [Fact]
    public void Issue_WithoutAnyItems_Fails()
    {
        var request = new IssuePrescriptionRequest(Guid.NewGuid(), Items: []);

        var result = _issueValidator.TestValidate(request);

        Assert.Contains(result.Errors, failure => failure.PropertyName == nameof(IssuePrescriptionRequest.Items));
    }

    [Fact]
    public void Issue_WithTooManyItems_Fails()
    {
        var items = Enumerable
            .Range(1, IssuePrescriptionRequestValidator.MaxItems + 1)
            .Select(index => new PrescriptionItemRequest($"Drug {index}", "500mg", TimesPerDay: 1, DurationDays: 7))
            .ToArray();

        var result = _issueValidator.TestValidate(new IssuePrescriptionRequest(Guid.NewGuid(), Items: items));

        Assert.Contains(result.Errors, failure => failure.PropertyName == nameof(IssuePrescriptionRequest.Items));
    }

    [Fact]
    public void Issue_WithAnInvalidItem_ReportsTheItemPath()
    {
        var request = new IssuePrescriptionRequest(
            Guid.NewGuid(),
            Items: [new PrescriptionItemRequest("Amoxicillin", "500mg", TimesPerDay: 0, DurationDays: 7)]);

        var result = _issueValidator.TestValidate(request);

        Assert.Contains(
            result.Errors,
            failure => failure.PropertyName.StartsWith("Items[", StringComparison.Ordinal) &&
                       failure.PropertyName.EndsWith(nameof(PrescriptionItemRequest.TimesPerDay), StringComparison.Ordinal));
    }

    [Fact]
    public void Item_WithAWellFormedShape_IsValid()
    {
        var item = new PrescriptionItemRequest(
            "Metformin",
            "1000mg",
            TimesPerDay: 2,
            DurationDays: 30,
            DoseTimes: [new TimeOnly(9, 0), new TimeOnly(21, 0)]);

        var result = _itemValidator.TestValidate(item);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(13)]
    public void Item_WithATimesPerDayOutsideTheAllowedRange_Fails(int timesPerDay)
    {
        var item = new PrescriptionItemRequest("Metformin", "1000mg", TimesPerDay: timesPerDay, DurationDays: 30);

        var result = _itemValidator.TestValidate(item);

        Assert.Contains(result.Errors, failure => failure.PropertyName == nameof(PrescriptionItemRequest.TimesPerDay));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(366)]
    public void Item_WithADurationOutsideTheAllowedRange_Fails(int durationDays)
    {
        var item = new PrescriptionItemRequest("Metformin", "1000mg", TimesPerDay: 2, DurationDays: durationDays);

        var result = _itemValidator.TestValidate(item);

        Assert.Contains(result.Errors, failure => failure.PropertyName == nameof(PrescriptionItemRequest.DurationDays));
    }

    [Fact]
    public void Item_WithADoseTimeCountThatDisagreesWithTimesPerDay_Fails()
    {
        var item = new PrescriptionItemRequest(
            "Metformin",
            "1000mg",
            TimesPerDay: 3,
            DurationDays: 30,
            DoseTimes: [new TimeOnly(9, 0), new TimeOnly(21, 0)]);

        var result = _itemValidator.TestValidate(item);

        Assert.Contains(result.Errors, failure => failure.PropertyName == string.Empty);
    }

    [Fact]
    public void Item_WithAnEmptyDrugNameOrDose_Fails()
    {
        var result = _itemValidator.TestValidate(new PrescriptionItemRequest(string.Empty, "  ", 1, 7));

        Assert.Contains(result.Errors, failure => failure.PropertyName == nameof(PrescriptionItemRequest.DrugName));
        Assert.Contains(result.Errors, failure => failure.PropertyName == nameof(PrescriptionItemRequest.Dose));
    }

    [Fact]
    public void Item_WithTextThatExceedsItsColumnLength_Fails()
    {
        var item = new PrescriptionItemRequest(
            new string('x', 201),
            new string('y', 201),
            TimesPerDay: 1,
            DurationDays: 7,
            Route: new string('z', 101),
            Instructions: new string('w', 1025));

        var result = _itemValidator.TestValidate(item);

        Assert.Contains(result.Errors, failure => failure.PropertyName == nameof(PrescriptionItemRequest.DrugName));
        Assert.Contains(result.Errors, failure => failure.PropertyName == nameof(PrescriptionItemRequest.Dose));
        Assert.Contains(result.Errors, failure => failure.PropertyName == nameof(PrescriptionItemRequest.Route));
        Assert.Contains(result.Errors, failure => failure.PropertyName == nameof(PrescriptionItemRequest.Instructions));
    }

    [Fact]
    public void Issue_WithNotesThatExceedTheirColumnLength_Fails()
    {
        var request = new IssuePrescriptionRequest(
            Guid.NewGuid(),
            Items: [new PrescriptionItemRequest("Metformin", "1000mg", TimesPerDay: 1, DurationDays: 30)],
            Notes: new string('n', 2049));

        var result = _issueValidator.TestValidate(request);

        Assert.Contains(result.Errors, failure => failure.PropertyName == nameof(IssuePrescriptionRequest.Notes));
    }

    [Fact]
    public void Search_WithTheDefaults_IsValid()
    {
        var result = _searchValidator.TestValidate(new PrescriptionSearchQuery());

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Search_WithAnInvertedRange_Fails()
    {
        var query = new PrescriptionSearchQuery
        {
            FromUtc = DateTime.UtcNow,
            ToUtc = DateTime.UtcNow.AddDays(-1)
        };

        var result = _searchValidator.TestValidate(query);

        Assert.Contains(result.Errors, failure => failure.PropertyName == string.Empty);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public void Search_WithAPageSizeOutsideTheAllowedRange_Fails(int pageSize)
    {
        var query = new PrescriptionSearchQuery { PageSize = pageSize };

        var result = _searchValidator.TestValidate(query);

        Assert.Contains(result.Errors, failure => failure.PropertyName == nameof(PrescriptionSearchQuery.PageSize));
    }

    [Fact]
    public void Search_WithAMaximumSizedPage_IsValid()
    {
        var query = new PrescriptionSearchQuery { Page = 2, PageSize = PageRequest.MaxPageSize };

        var result = _searchValidator.TestValidate(query);

        result.ShouldNotHaveAnyValidationErrors();
    }
}
