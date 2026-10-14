using ClinicManagement.Domain.Common;

namespace ClinicManagement.Domain.Users;

public sealed class Doctor
{
    private readonly List<Appointments.DoctorSchedule> _schedules = [];
    private readonly List<Appointments.Appointment> _appointments = [];
    private readonly List<Patients.PatientVisit> _visits = [];
    private readonly List<Prescriptions.Prescription> _prescriptions = [];

    private Doctor()
    {
    }

    public Guid Id { get; private set; }

    public Guid ApplicationUserId { get; private set; }

    public string Specialty { get; private set; } = string.Empty;

    public string LicenseNumber { get; private set; } = string.Empty;

    public bool IsAcceptingAppointments { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public IReadOnlyCollection<Appointments.DoctorSchedule> Schedules => _schedules;

    public IReadOnlyCollection<Appointments.Appointment> Appointments => _appointments;

    public IReadOnlyCollection<Patients.PatientVisit> Visits => _visits;

    public IReadOnlyCollection<Prescriptions.Prescription> Prescriptions => _prescriptions;

    public static Doctor Create(Guid applicationUserId, string specialty, string licenseNumber, DateTime nowUtc)
    {
        if (applicationUserId == Guid.Empty)
        {
            throw new DomainException(ErrorCodes.ValidationFailed, "applicationUserId is required.");
        }

        if (string.IsNullOrWhiteSpace(specialty))
        {
            throw new DomainException(ErrorCodes.ValidationFailed, "specialty is required.");
        }

        if (string.IsNullOrWhiteSpace(licenseNumber))
        {
            throw new DomainException(ErrorCodes.ValidationFailed, "licenseNumber is required.");
        }

        return new Doctor
        {
            ApplicationUserId = applicationUserId,
            Specialty = specialty.Trim(),
            LicenseNumber = licenseNumber.Trim(),
            IsAcceptingAppointments = true,
            CreatedAtUtc = nowUtc
        };
    }

    public void SetAcceptingAppointments(bool accepting)
    {
        IsAcceptingAppointments = accepting;
    }

    public void UpdateProfile(string specialty, string licenseNumber)
    {
        if (string.IsNullOrWhiteSpace(specialty))
        {
            throw new DomainException(ErrorCodes.ValidationFailed, "specialty is required.");
        }

        if (string.IsNullOrWhiteSpace(licenseNumber))
        {
            throw new DomainException(ErrorCodes.ValidationFailed, "licenseNumber is required.");
        }

        Specialty = specialty.Trim();
        LicenseNumber = licenseNumber.Trim();
    }
}