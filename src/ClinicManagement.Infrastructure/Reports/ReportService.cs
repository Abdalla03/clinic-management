using ClinicManagement.Application.Common;
using ClinicManagement.Application.Reports;
using ClinicManagement.Domain.Billing;
using ClinicManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ClinicManagement.Infrastructure.Reports;

public sealed class ReportService(
    ClinicDbContext dbContext,
    TimeProvider timeProvider) : IReportService
{
    private const int DefaultRangeDays = 30;

    public async Task<Result<RevenueReport>> GetRevenueAsync(ReportRangeQuery query, CancellationToken cancellationToken)
    {
        var (fromUtc, toUtc) = ResolveRange(query);

        var payments = await dbContext.Payments.AsNoTracking()
            .Where(payment => payment.PaidAtUtc >= fromUtc && payment.PaidAtUtc <= toUtc)
            .ToListAsync(cancellationToken);

        var byMethod = payments
            .GroupBy(payment => payment.Method)
            .OrderBy(group => group.Key)
            .Select(group => new RevenueByMethod(group.Key, Sum(group.Select(payment => payment.Amount)), group.Count()))
            .ToArray();

        var invoiceCount = await dbContext.Invoices.AsNoTracking()
            .CountAsync(
                invoice => invoice.Payments.Any(payment =>
                    payment.PaidAtUtc >= fromUtc && payment.PaidAtUtc <= toUtc),
                cancellationToken);

        var report = new RevenueReport(
            fromUtc,
            toUtc,
            Sum(payments.Select(payment => payment.Amount)),
            invoiceCount,
            payments.Count,
            byMethod);

        return Result<RevenueReport>.Success(report);
    }

    public async Task<Result<OutstandingReport>> GetOutstandingAsync(
        ReportRangeQuery query,
        CancellationToken cancellationToken)
    {
        var (fromUtc, toUtc) = ResolveRange(query);

        var invoices = await dbContext.Invoices.AsNoTracking()
            .Include(invoice => invoice.Patient)
            .Where(invoice => invoice.Status != InvoiceStatus.Void && invoice.Status != InvoiceStatus.Paid)
            .Where(invoice => invoice.IssuedAtUtc <= toUtc)
            .ToListAsync(cancellationToken);

        var byPatient = invoices
            .GroupBy(invoice => invoice.PatientId)
            .Select(group =>
            {
                var patient = group.First().Patient;
                return new OutstandingByPatient(
                    group.Key,
                    patient?.FullName ?? string.Empty,
                    patient?.MedicalRecordNumber ?? string.Empty,
                    Sum(group.Select(invoice => invoice.TotalAmount - invoice.AmountPaid)),
                    group.Count());
            })
            .OrderByDescending(item => item.OutstandingAmount)
            .ThenBy(item => item.PatientName, StringComparer.Ordinal)
            .ToArray();

        var report = new OutstandingReport(
            fromUtc,
            toUtc,
            Sum(byPatient.Select(item => item.OutstandingAmount)),
            byPatient);

        return Result<OutstandingReport>.Success(report);
    }

    public async Task<Result<VisitReport>> GetVisitReportAsync(
        ReportRangeQuery query,
        CancellationToken cancellationToken)
    {
        var (fromUtc, toUtc) = ResolveRange(query);

        var visits = await dbContext.PatientVisits.AsNoTracking()
            .Where(visit => visit.VisitAtUtc >= fromUtc && visit.VisitAtUtc <= toUtc)
            .Select(visit => new { visit.DoctorId })
            .ToListAsync(cancellationToken);

        var doctorIds = visits.Select(visit => visit.DoctorId).Distinct().ToArray();

        var doctorNames = await (
            from doctor in dbContext.Doctors.AsNoTracking()
            join user in dbContext.Users.AsNoTracking()
                on doctor.ApplicationUserId equals user.Id
            where doctorIds.Contains(doctor.Id)
            select new { doctor.Id, user.FullName })
            .ToDictionaryAsync(entry => entry.Id, entry => entry.FullName, cancellationToken);

        var byDoctor = visits
            .GroupBy(visit => visit.DoctorId)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => doctorNames.TryGetValue(group.Key, out var name) ? name : string.Empty,
                StringComparer.Ordinal)
            .Select(group => new VisitsByDoctor(
                group.Key,
                doctorNames.TryGetValue(group.Key, out var name) ? name : string.Empty,
                group.Count()))
            .ToArray();

        var report = new VisitReport(fromUtc, toUtc, visits.Count, byDoctor);

        return Result<VisitReport>.Success(report);
    }

    private (DateTime FromUtc, DateTime ToUtc) ResolveRange(ReportRangeQuery query)
    {
        var toUtc = Normalize(query.ToUtc ?? timeProvider.GetUtcNow().UtcDateTime);

        var fromUtc = Normalize(query.FromUtc ?? toUtc.AddDays(-DefaultRangeDays));

        if (fromUtc > toUtc)
        {
            (fromUtc, toUtc) = (toUtc, fromUtc);
        }

        return (fromUtc, toUtc);
    }

    private static DateTime Normalize(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };

    private static decimal Sum(IEnumerable<decimal> values) =>
        Math.Round(values.Sum(), 2, MidpointRounding.AwayFromZero);
}
