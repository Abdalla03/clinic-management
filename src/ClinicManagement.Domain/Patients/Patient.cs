using ClinicManagement.Domain.Common;
using ClinicManagement.Domain.Users;

namespace ClinicManagement.Domain.Patients;

public sealed class Patient
{
    private readonly List<Allergy> _allergies = [];
    private readonly List<MedicalHistoryEntry> _medicalHistory = [];
    private readonly List<PatientVisit> _visits = [];
    private readonly List<PatientAttachment> _attachments = [];
    private readonly List<Appointments.Appointment> _appointments = [];
    private readonly List<Prescriptions.Prescription> _prescriptions = [];

    private Patient()
    {
    }

    public Guid Id { get; private set; }

    public string MedicalRecordNumber { get; private set; } = string.Empty;

    public string FirstName { get; private set; } = string.Empty;

    public string LastName { get; private set; } = string.Empty;

    [EncryptedColumn]
    public string? NationalId { get; private set; }

    [EncryptedColumn]
    public string? PhoneNumber { get; private set; }

    [EncryptedColumn]
    public string? EmergencyContactPhone { get; private set; }

    public string? EmergencyContactName { get; private set; }

    public DateOnly? DateOfBirth { get; private set; }

    public Gender? Gender { get; private set; }

    public BloodType? BloodType { get; private set; }

    [EncryptedColumn]
    public string? Address { get; private set; }

    [EncryptedColumn]
    public string? Notes { get; private set; }

    public bool IsActive { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

    public Guid? ApplicationUserId { get; private set; }

    public Guid Version { get; private set; }

    public string FullName => $"{FirstName} {LastName}".Trim();

    public IReadOnlyCollection<Allergy> Allergies => _allergies;

    public IReadOnlyCollection<MedicalHistoryEntry> MedicalHistory => _medicalHistory;

    public IReadOnlyCollection<PatientVisit> Visits => _visits;

    public IReadOnlyCollection<PatientAttachment> Attachments => _attachments;

    public IReadOnlyCollection<Appointments.Appointment> Appointments => _appointments;

    public IReadOnlyCollection<Prescriptions.Prescription> Prescriptions => _prescriptions;

    public static Patient Create(
        string medicalRecordNumber,
        string firstName,
        string lastName,
        DateTime nowUtc,
        Guid? applicationUserId = null)
    {
        ValidateName(firstName, lastName);

        if (string.IsNullOrWhiteSpace(medicalRecordNumber))
        {
            throw new DomainException(ErrorCodes.ValidationFailed, "medicalRecordNumber is required.");
        }

        return new Patient
        {
            MedicalRecordNumber = medicalRecordNumber.Trim(),
            FirstName = firstName.Trim(),
            LastName = lastName.Trim(),
            IsActive = true,
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc,
            ApplicationUserId = applicationUserId,
            Version = Guid.NewGuid()
        };
    }

    public void Rename(string firstName, string lastName, DateTime nowUtc)
    {
        ValidateName(firstName, lastName);
        FirstName = firstName.Trim();
        LastName = lastName.Trim();
        Touch(nowUtc);
    }

    public void UpdateContactDetails(
        string? nationalId,
        string? phoneNumber,
        string? emergencyContactName,
        string? emergencyContactPhone,
        DateTime nowUtc)
    {
        NationalId = Normalize(nationalId);
        PhoneNumber = Normalize(phoneNumber);
        EmergencyContactName = Normalize(emergencyContactName);
        EmergencyContactPhone = Normalize(emergencyContactPhone);
        Touch(nowUtc);
    }

    public void UpdateDemographics(DateOnly? dateOfBirth, Gender? gender, BloodType? bloodType, DateTime nowUtc)
    {
        if (dateOfBirth is { } dob && dob > DateOnly.FromDateTime(nowUtc))
        {
            throw new DomainException(ErrorCodes.ValidationFailed, "dateOfBirth cannot be in the future.");
        }

        DateOfBirth = dateOfBirth;
        Gender = gender;
        BloodType = bloodType;
        Touch(nowUtc);
    }

    public void UpdateNotes(string? notes, DateTime nowUtc)
    {
        Notes = Normalize(notes);
        Touch(nowUtc);
    }

    public void LinkToAccount(Guid applicationUserId, DateTime nowUtc)
    {
        if (ApplicationUserId is not null && ApplicationUserId != applicationUserId)
        {
            throw new DomainException(ErrorCodes.ConcurrencyConflict, "Patient is already linked to a different account.");
        }

        ApplicationUserId = applicationUserId;
        Touch(nowUtc);
    }

    public void Deactivate(DateTime nowUtc)
    {
        IsActive = false;
        Touch(nowUtc);
    }

    public void Touch(DateTime nowUtc)
    {
        UpdatedAtUtc = nowUtc;
        Version = Guid.NewGuid();
    }

    private static void ValidateName(string firstName, string lastName)
    {
        if (string.IsNullOrWhiteSpace(firstName))
        {
            throw new DomainException(ErrorCodes.ValidationFailed, "firstName is required.");
        }

        if (string.IsNullOrWhiteSpace(lastName))
        {
            throw new DomainException(ErrorCodes.ValidationFailed, "lastName is required.");
        }
    }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}