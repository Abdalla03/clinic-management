using ClinicManagement.Domain.Billing;

namespace ClinicManagement.Application.Reports;

public sealed record ReportRangeQuery(DateTime? FromUtc = null, DateTime? ToUtc = null);

public sealed record RevenueByMethod(PaymentMethod Method, decimal Amount, int PaymentCount);

public sealed record RevenueReport(
    DateTime FromUtc,
    DateTime ToUtc,
    decimal TotalCollected,
    int InvoiceCount,
    int PaymentCount,
    IReadOnlyList<RevenueByMethod> ByMethod);

public sealed record OutstandingByPatient(
    Guid PatientId,
    string PatientName,
    string MedicalRecordNumber,
    decimal OutstandingAmount,
    int InvoiceCount);

public sealed record OutstandingReport(
    DateTime FromUtc,
    DateTime ToUtc,
    decimal TotalOutstanding,
    IReadOnlyList<OutstandingByPatient> ByPatient);

public sealed record VisitsByDoctor(Guid DoctorId, string DoctorName, int VisitCount);

public sealed record VisitReport(
    DateTime FromUtc,
    DateTime ToUtc,
    int TotalVisits,
    IReadOnlyList<VisitsByDoctor> ByDoctor);
