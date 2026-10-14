using ClinicManagement.Application.Common.Paging;

namespace ClinicManagement.Application.Visits;

public sealed record VisitSummary(
    Guid Id,
    Guid PatientId,
    string PatientName,
    Guid DoctorId,
    string DoctorName,
    Guid? AppointmentId,
    DateTime VisitAtUtc,
    string? Diagnosis,
    DateTime CreatedAtUtc);

public sealed record VisitDetails(
    Guid Id,
    Guid PatientId,
    string PatientName,
    Guid DoctorId,
    string DoctorName,
    Guid? AppointmentId,
    DateTime VisitAtUtc,
    string? ChiefComplaint,
    string? Notes,
    string? Diagnosis,
    Guid RecordedByUserId,
    DateTime CreatedAtUtc);

public sealed record RecordVisitRequest(
    Guid PatientId,
    Guid DoctorId,
    DateTime VisitAtUtc,
    Guid? AppointmentId = null,
    string? ChiefComplaint = null,
    string? Notes = null,
    string? Diagnosis = null);

public sealed record UpdateVisitRequest(
    string? ChiefComplaint = null,
    string? Notes = null,
    string? Diagnosis = null);

public sealed record VisitSearchQuery
{
    public Guid? PatientId { get; init; }

    public Guid? DoctorId { get; init; }

    public DateTime? FromUtc { get; init; }

    public DateTime? ToUtc { get; init; }

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = PageRequest.DefaultPageSize;
}
