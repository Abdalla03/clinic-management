using ClinicManagement.Application.Patients;
using ClinicManagement.Application.Patients.Validation;
using ClinicManagement.Domain.Patients;
using FluentValidation.TestHelper;
using Xunit;

namespace ClinicManagement.UnitTests.Patients;

public sealed class PatientValidatorTests
{
    private readonly CreatePatientRequestValidator _createValidator = new();
    private readonly UpdatePatientRequestValidator _updateValidator = new();
    private readonly PatientSearchQueryValidator _searchValidator = new();
    private readonly AddMedicalHistoryRequestValidator _historyValidator = new();

    [Fact]
    public void Create_WithADocumentedDemoStyleRecord_IsValid()
    {
        var request = new CreatePatientRequest(
            "MRN-DEMO-0002",
            "Salma",
            "Hassan",
            NationalId: "12345678901234",
            PhoneNumber: "+201001112233",
            DateOfBirth: new DateOnly(1990, 4, 2),
            Gender: Gender.Female,
            BloodType: BloodType.OPositive,
            Address: "12 Nile St, Cairo",
            EmergencyContactName: "Omar Hassan",
            EmergencyContactPhone: "+201112223333",
            Notes: "Routine registration");

        var result = _createValidator.TestValidate(request);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("MRN has spaces")]
    [InlineData("MRN;DROP TABLE")]
    public void Create_WithAnInvalidMedicalRecordNumber_Fails(string medicalRecordNumber)
    {
        var request = new CreatePatientRequest(medicalRecordNumber, "Salma", "Hassan");

        var result = _createValidator.TestValidate(request);

        Assert.Contains(result.Errors, failure => failure.PropertyName == nameof(CreatePatientRequest.MedicalRecordNumber));
    }

    [Fact]
    public void Create_WithAnOverlongMedicalRecordNumber_Fails()
    {
        var request = new CreatePatientRequest(new string('A', 33), "Salma", "Hassan");

        var result = _createValidator.TestValidate(request);

        Assert.Contains(result.Errors, failure => failure.PropertyName == nameof(CreatePatientRequest.MedicalRecordNumber));
    }

    [Fact]
    public void Create_WithAFutureDateOfBirth_Fails()
    {
        var request = new CreatePatientRequest(
            "MRN-1",
            "Salma",
            "Hassan",
            DateOfBirth: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)));

        var result = _createValidator.TestValidate(request);

        Assert.Contains(
            result.Errors,
            failure => failure.PropertyName == nameof(CreatePatientRequest.DateOfBirth));
    }

    [Theory]
    [InlineData("", "Hassan")]
    [InlineData("Salma", "")]
    public void Create_WithABlankName_Fails(string firstName, string lastName)
    {
        var request = new CreatePatientRequest("MRN-1", firstName, lastName);

        var result = _createValidator.TestValidate(request);

        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public void Create_WithAnOverlongNotes_Fails()
    {
        var request = new CreatePatientRequest("MRN-1", "Salma", "Hassan", Notes: new string('n', 1025));

        var result = _createValidator.TestValidate(request);

        Assert.Contains(result.Errors, failure => failure.PropertyName == nameof(CreatePatientRequest.Notes));
    }

    [Fact]
    public void Update_WithABlankLastName_Fails()
    {
        var request = new UpdatePatientRequest("Salma", "  ");

        var result = _updateValidator.TestValidate(request);

        Assert.Contains(result.Errors, failure => failure.PropertyName == nameof(UpdatePatientRequest.LastName));
    }

    [Fact]
    public void Update_WithAFutureDateOfBirth_Fails()
    {
        var request = new UpdatePatientRequest(
            "Salma",
            "Hassan",
            DateOfBirth: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3)));

        var result = _updateValidator.TestValidate(request);

        Assert.Contains(
            result.Errors,
            failure => failure.PropertyName == nameof(UpdatePatientRequest.DateOfBirth));
    }

    [Fact]
    public void Search_WithTheDefaultPaging_IsValid()
    {
        var result = _searchValidator.TestValidate(new PatientSearchQuery());

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Search_WithAPageSizeAboveTheMaximum_Fails()
    {
        var result = _searchValidator.TestValidate(new PatientSearchQuery { PageSize = 101 });

        Assert.Contains(result.Errors, failure => failure.PropertyName == nameof(PatientSearchQuery.PageSize));
    }

    [Fact]
    public void Search_WithPageZero_Fails()
    {
        var result = _searchValidator.TestValidate(new PatientSearchQuery { Page = 0 });

        Assert.Contains(result.Errors, failure => failure.PropertyName == nameof(PatientSearchQuery.Page));
    }

    [Fact]
    public void Search_WithAnOverlongQuery_Fails()
    {
        var result = _searchValidator.TestValidate(new PatientSearchQuery { Query = new string('q', 201) });

        Assert.Contains(result.Errors, failure => failure.PropertyName == nameof(PatientSearchQuery.Query));
    }

    [Fact]
    public void Search_WithANullQuery_IsValid()
    {
        var result = _searchValidator.TestValidate(new PatientSearchQuery { Query = null });

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void MedicalHistory_WithAConditionName_IsValid()
    {
        var result = _historyValidator.TestValidate(new AddMedicalHistoryRequest("Type 2 diabetes", new DateOnly(2019, 6, 1)));

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void MedicalHistory_WithoutAConditionName_Fails()
    {
        var result = _historyValidator.TestValidate(new AddMedicalHistoryRequest("   "));

        Assert.Contains(
            result.Errors,
            failure => failure.PropertyName == nameof(AddMedicalHistoryRequest.ConditionName));
    }

    [Fact]
    public void MedicalHistory_WithAnOverlongConditionName_Fails()
    {
        var result = _historyValidator.TestValidate(new AddMedicalHistoryRequest(new string('c', 201)));

        Assert.Contains(
            result.Errors,
            failure => failure.PropertyName == nameof(AddMedicalHistoryRequest.ConditionName));
    }
}
