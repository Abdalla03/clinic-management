using ClinicManagement.Application.Common.Paging;
using ClinicManagement.Domain.Prescriptions;

namespace ClinicManagement.Application.Prescriptions;

public sealed record PrescriptionItemResponse(
    Guid Id,
    string DrugName,
    string Dose,
    string? Route,
    int TimesPerDay,
    int DurationDays,
    string? Instructions,
    IReadOnlyList<TimeOnly> DoseTimes,
    AllergyCheckOutcome AllergyCheckOutcome,
    Guid? MatchedAllergyId);

public sealed record PrescriptionSummary(
    Guid Id,
    Guid PatientId,
    string PatientName,
    Guid DoctorId,
    string DoctorName,
    Guid? VisitId,
    DateTime IssuedAtUtc,
    string? Notes,
    int ItemCount,
    bool ContainsAllergyMatch);

public sealed record PrescriptionDetails(
    Guid Id,
    Guid PatientId,
    string PatientName,
    Guid DoctorId,
    string DoctorName,
    Guid? VisitId,
    DateTime IssuedAtUtc,
    string? Notes,
    IReadOnlyList<PrescriptionItemResponse> Items);

public sealed record PrescriptionItemRequest(
    string DrugName,
    string Dose,
    int TimesPerDay,
    int DurationDays,
    string? Route = null,
    string? Instructions = null,
    IReadOnlyList<TimeOnly>? DoseTimes = null);

public sealed record IssuePrescriptionRequest(
    Guid PatientId,
    IReadOnlyList<PrescriptionItemRequest> Items,
    Guid? VisitId = null,
    string? Notes = null);

public sealed record PrescriptionSearchQuery
{
    public Guid? PatientId { get; init; }

    public Guid? DoctorId { get; init; }

    public Guid? VisitId { get; init; }

    public DateTime? FromUtc { get; init; }

    public DateTime? ToUtc { get; init; }

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = PageRequest.DefaultPageSize;
}
