using ClinicManagement.Domain.Common;
using ClinicManagement.Domain.Patients;
using Xunit;

namespace ClinicManagement.UnitTests.Patients;

public sealed class PatientTests
{
    private static readonly DateTime Now = new(2026, 3, 14, 8, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Create_InitialisesAnActiveRecord()
    {
        var patient = Patient.Create("MRN-0001", "Salma", "Hassan", Now);

        Assert.True(patient.IsActive);
        Assert.Equal("Salma", patient.FirstName);
        Assert.Equal("Hassan", patient.LastName);
        Assert.Equal("Salma Hassan", patient.FullName);
        Assert.Equal(Now, patient.CreatedAtUtc);
        Assert.NotEqual(Guid.Empty, patient.Version);
    }

    [Fact]
    public void Create_WithABlankMedicalRecordNumber_Throws()
    {
        var exception = Assert.Throws<DomainException>(() => Patient.Create("  ", "Salma", "Hassan", Now));

        Assert.Equal(ErrorCodes.ValidationFailed, exception.Code);
    }

    [Fact]
    public void Rename_UpdatesTheNameAndBumpsTheVersion()
    {
        var patient = Patient.Create("MRN-0001", "Salma", "Hassan", Now);
        var previousVersion = patient.Version;
        var later = Now.AddMinutes(5);

        patient.Rename("Salma", "El-Hassan", later);

        Assert.Equal("Salma", patient.FirstName);
        Assert.Equal("El-Hassan", patient.LastName);
        Assert.Equal("Salma El-Hassan", patient.FullName);
        Assert.Equal(later, patient.UpdatedAtUtc);
        Assert.NotEqual(previousVersion, patient.Version);
    }

    [Theory]
    [InlineData("", "Hassan")]
    [InlineData("Salma", "")]
    public void Rename_WithABlankName_Throws(string firstName, string lastName)
    {
        var patient = Patient.Create("MRN-0001", "Salma", "Hassan", Now);

        var exception = Assert.Throws<DomainException>(() => patient.Rename(firstName, lastName, Now));

        Assert.Equal(ErrorCodes.ValidationFailed, exception.Code);
    }

    [Fact]
    public void UpdateDemographics_WithAFutureDateOfBirth_Throws()
    {
        var patient = Patient.Create("MRN-0001", "Salma", "Hassan", Now);

        var exception = Assert.Throws<DomainException>(() => patient.UpdateDemographics(
            DateOnly.FromDateTime(Now.AddDays(1)),
            Gender.Female,
            BloodType.OPositive,
            Now));

        Assert.Equal(ErrorCodes.ValidationFailed, exception.Code);
    }

    [Fact]
    public void UpdateContactDetails_TrimsAndClearsBlankValues()
    {
        var patient = Patient.Create("MRN-0001", "Salma", "Hassan", Now);

        patient.UpdateContactDetails("  ", " +201001112233 ", "Omar", "", Now.AddMinutes(1));

        Assert.Null(patient.NationalId);
        Assert.Equal("+201001112233", patient.PhoneNumber);
        Assert.Equal("Omar", patient.EmergencyContactName);
        Assert.Null(patient.EmergencyContactPhone);
    }

    [Fact]
    public void Deactivate_MarksTheRecordInactive()
    {
        var patient = Patient.Create("MRN-0001", "Salma", "Hassan", Now);

        patient.Deactivate(Now.AddMinutes(1));

        Assert.False(patient.IsActive);
    }

    [Fact]
    public void LinkToAccount_ASecondTimeForTheSameAccountIsAccepted()
    {
        var patient = Patient.Create("MRN-0001", "Salma", "Hassan", Now);
        var accountId = Guid.NewGuid();

        patient.LinkToAccount(accountId, Now);
        patient.LinkToAccount(accountId, Now.AddMinutes(1));

        Assert.Equal(accountId, patient.ApplicationUserId);
    }

    [Fact]
    public void LinkToAccount_ADifferentAccount_Throws()
    {
        var patient = Patient.Create("MRN-0001", "Salma", "Hassan", Now);
        patient.LinkToAccount(Guid.NewGuid(), Now);

        var exception = Assert.Throws<DomainException>(() => patient.LinkToAccount(Guid.NewGuid(), Now));

        Assert.Equal(ErrorCodes.ConcurrencyConflict, exception.Code);
    }
}
