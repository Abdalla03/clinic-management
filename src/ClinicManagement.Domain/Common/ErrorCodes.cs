namespace ClinicManagement.Domain.Common;

public static class ErrorCodes
{
    public const string ConcurrencyConflict = "concurrency.conflict";
    public const string InvalidStateTransition = "state.invalid_transition";
    public const string ValidationFailed = "validation.failed";
    public const string NotFound = "not_found";
    public const string Forbidden = "forbidden";

    /// <summary>The request is well formed but collides with existing state (duplicate record, taken slot).</summary>
    public const string Conflict = "conflict";

    /// <summary>The requested appointment slot is already occupied for that doctor.</summary>
    public const string AppointmentSlotTaken = "appointment.slot_taken";

    /// <summary>The requested time overlaps another appointment for the same patient or doctor.</summary>
    public const string AppointmentOverlap = "appointment.overlap";

    /// <summary>The requested time falls outside the doctor's working hours.</summary>
    public const string AppointmentOutsideWorkingHours = "appointment.outside_working_hours";
}
