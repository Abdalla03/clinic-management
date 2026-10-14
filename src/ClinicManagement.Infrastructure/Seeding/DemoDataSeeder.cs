using ClinicManagement.Domain.Appointments;
using ClinicManagement.Domain.Billing;
using ClinicManagement.Domain.Common;
using ClinicManagement.Domain.Patients;
using ClinicManagement.Domain.Prescriptions;
using ClinicManagement.Domain.Users;
using ClinicManagement.Infrastructure.Identity;
using ClinicManagement.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ClinicManagement.Infrastructure.Seeding;

/// <summary>
/// Creates a realistic, development-only data set on top of the demo accounts: extra doctors with
/// working hours, eight patients with allergies and history, a spread of past/upcoming/cancelled/
/// no-show appointments, visits, prescriptions and invoices in every state. It is idempotent: if the
/// marker doctor already exists the seeder does nothing, and individual inserts are guarded so a
/// half-seeded database can still be topped up safely.
/// </summary>
public sealed class DemoDataSeeder(
    ClinicDbContext dbContext,
    UserManager<ApplicationUser> userManager,
    ILogger<DemoDataSeeder> logger)
{
    private const string Currency = "EGP";
    private const string MarkerLicense = "LIC-DEMO-0002";

    private static readonly TimeOnly MorningStart = new(9, 0);
    private static readonly TimeOnly MorningEnd = new(13, 0);
    private static readonly TimeOnly AfternoonStart = new(14, 0);
    private static readonly TimeOnly AfternoonEnd = new(17, 0);
    private const int SlotMinutes = 30;

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        // Every insert below is guarded by an existence check, so the seeder can run on every boot and
        // can safely resume after a partial run (for example if a previous boot was interrupted).
        var secretaryUserId = await ResolveUserIdAsync(DemoAccounts.SecretaryEmail, cancellationToken);
        var demoDoctorUserId = await ResolveUserIdAsync(DemoAccounts.DoctorEmail, cancellationToken);

        var extraDoctors = new (string Email, string FullName, string Specialty, string License)[]
        {
            ("doctor.pediatrics@clinic.local", "Dr. Omar Khaled", "Pediatrics", MarkerLicense),
            ("doctor.dermatology@clinic.local", "Dr. Laila Mansour", "Dermatology", "LIC-DEMO-0003"),
            ("doctor.orthopedics@clinic.local", "Dr. Youssef Nabil", "Orthopedics", "LIC-DEMO-0004")
        };

        var doctorIds = new List<Guid>();
        var doctorUserIds = new List<Guid>();

        var primaryDoctorId = await dbContext.Doctors
            .Where(doctor => doctor.LicenseNumber == DemoAccounts.DoctorLicenseNumber)
            .Select(doctor => doctor.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (primaryDoctorId != Guid.Empty)
        {
            doctorIds.Add(primaryDoctorId);
            doctorUserIds.Add(demoDoctorUserId);
        }

        foreach (var spec in extraDoctors)
        {
            var userId = await EnsureUserAsync(spec.Email, spec.FullName, RoleNames.Doctor, cancellationToken);
            if (userId == Guid.Empty)
            {
                continue;
            }

            var doctor = await dbContext.Doctors
                .FirstOrDefaultAsync(candidate => candidate.ApplicationUserId == userId, cancellationToken);

            if (doctor is null)
            {
                doctor = Doctor.Create(userId, spec.Specialty, spec.License, now);
                dbContext.Doctors.Add(doctor);
                await dbContext.SaveChangesAsync(cancellationToken);
                logger.LogInformation("Seeded demo doctor {Name} ({Specialty}).", spec.FullName, spec.Specialty);
            }

            doctorIds.Add(doctor.Id);
            doctorUserIds.Add(userId);
        }

        if (doctorIds.Count == 0)
        {
            logger.LogWarning("No doctors were available; rich demo data seeding was skipped.");
            return;
        }

        foreach (var doctorId in doctorIds)
        {
            await EnsureScheduleAsync(doctorId, now, cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        var recordedBy = demoDoctorUserId != Guid.Empty ? demoDoctorUserId : doctorUserIds[0];
        var billingUser = secretaryUserId != Guid.Empty ? secretaryUserId : recordedBy;

        // ----- Patients -----
        var ahmed = await EnsurePatientAsync("MRN-DEMO-1001", "Ahmed", "Hassan", new DateOnly(1985, 2, 11), Gender.Male, BloodType.OPositive, "+201000000010", "28502110100411", "Prefers morning appointments.", now, cancellationToken);
        var mona = await EnsurePatientAsync("MRN-DEMO-1002", "Mona", "Adel", new DateOnly(1990, 7, 23), Gender.Female, BloodType.APositive, "+201000000011", "29007230100422", null, now, cancellationToken);
        var khaled = await EnsurePatientAsync("MRN-DEMO-1003", "Khaled", "Samir", new DateOnly(1978, 11, 5), Gender.Male, BloodType.BPositive, "+201000000012", "27811050100433", null, now, cancellationToken);
        var huda = await EnsurePatientAsync("MRN-DEMO-1004", "Huda", "Rashad", new DateOnly(2001, 3, 30), Gender.Female, BloodType.AbPositive, "+201000000013", "20103300100444", "Student.", now, cancellationToken);
        var tarek = await EnsurePatientAsync("MRN-DEMO-1005", "Tarek", "Fouad", new DateOnly(1969, 9, 14), Gender.Male, BloodType.ONegative, "+201000000014", "26909140100455", "Long-term hypertension follow-up.", now, cancellationToken);
        var salma = await EnsurePatientAsync("MRN-DEMO-1006", "Salma", "Nabil", new DateOnly(1995, 12, 2), Gender.Female, BloodType.APositive, "+201000000015", "29512020100466", null, now, cancellationToken);
        var yara = await EnsurePatientAsync("MRN-DEMO-1007", "Yara", "Magdy", new DateOnly(2008, 6, 18), Gender.Female, BloodType.OPositive, "+201000000016", "30806180100477", "Pediatric patient.", now, cancellationToken);
        var mostafa = await EnsurePatientAsync("MRN-DEMO-1008", "Mostafa", "Kamel", new DateOnly(1958, 1, 27), Gender.Male, BloodType.BNegative, "+201000000017", "25801270100488", "Cardiac history.", now, cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        await EnsureAllergyAsync(ahmed, "Penicillin", "Anaphylaxis", AllergySeverity.Severe, recordedBy, now, cancellationToken);
        await EnsureAllergyAsync(mona, "Aspirin", "Rash", AllergySeverity.Moderate, recordedBy, now, cancellationToken);
        await EnsureAllergyAsync(tarek, "Sulfa drugs", "Angioedema", AllergySeverity.Severe, recordedBy, now, cancellationToken);

        await EnsureHistoryAsync(tarek, "Hypertension", new DateOnly(2019, 5, 1), recordedBy, now, cancellationToken);
        await EnsureHistoryAsync(tarek, "Type 2 Diabetes", new DateOnly(2016, 3, 12), recordedBy, now, cancellationToken);
        await EnsureHistoryAsync(mostafa, "Coronary artery disease", new DateOnly(2015, 8, 20), recordedBy, now, cancellationToken);
        await EnsureHistoryAsync(khaled, "Asthma", new DateOnly(2005, 1, 10), recordedBy, now, cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        var demoDoctorId = primaryDoctorId != Guid.Empty ? primaryDoctorId : doctorIds[0];
        var secondDoctorId = doctorIds.Count > 1 ? doctorIds[1] : demoDoctorId;
        var thirdDoctorId = doctorIds.Count > 2 ? doctorIds[2] : secondDoctorId;
        var fourthDoctorId = doctorIds.Count > 3 ? doctorIds[3] : thirdDoctorId;

        var demoPatientId = await dbContext.Patients
            .Where(patient => patient.MedicalRecordNumber == DemoAccounts.PatientMedicalRecordNumber)
            .Select(patient => patient.Id)
            .FirstOrDefaultAsync(cancellationToken);

        // ----- Upcoming appointments (booked/confirmed) -----
        if (demoPatientId != Guid.Empty)
        {
            await EnsureBlockingAsync(demoPatientId, demoDoctorId, UtcAt(now, 2, 9, 0), 30, "Follow-up consultation", confirmed: true, recordedBy, now, cancellationToken);
        }

        await EnsureBlockingAsync(ahmed, demoDoctorId, UtcAt(now, 3, 10, 0), 30, "Chest pain review", confirmed: false, recordedBy, now, cancellationToken);
        await EnsureBlockingAsync(mona, secondDoctorId, UtcAt(now, 2, 9, 30), 30, "Child vaccination", confirmed: false, recordedBy, now, cancellationToken);
        await EnsureBlockingAsync(khaled, thirdDoctorId, UtcAt(now, 4, 11, 0), 30, "Skin rash", confirmed: true, recordedBy, now, cancellationToken);
        await EnsureBlockingAsync(salma, fourthDoctorId, UtcAt(now, 5, 9, 0), 30, "Knee pain", confirmed: false, recordedBy, now, cancellationToken);
        await EnsureBlockingAsync(yara, secondDoctorId, UtcAt(now, 6, 14, 0), 30, "Fever and cough", confirmed: false, recordedBy, now, cancellationToken);
        await EnsureBlockingAsync(mostafa, demoDoctorId, UtcAt(now, 7, 9, 30), 30, "Cardiac review", confirmed: true, recordedBy, now, cancellationToken);

        // ----- Cancelled and no-show appointments -----
        await EnsureTransitionedAsync(huda, thirdDoctorId, UtcAt(now, 3, 15, 0), 30, "Skin consultation", AppointmentStatus.Cancelled, "Patient rescheduled", recordedBy, now, cancellationToken);
        await EnsureTransitionedAsync(tarek, demoDoctorId, UtcAt(now, -5, 10, 0), 30, "Blood pressure check", AppointmentStatus.Cancelled, "Doctor emergency", recordedBy, now, cancellationToken);
        await EnsureTransitionedAsync(huda, demoDoctorId, UtcAt(now, -3, 11, 0), 30, "General check", AppointmentStatus.NoShow, null, recordedBy, now, cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        // ----- Past completed visits, prescriptions and invoices -----
        var ahmedVisit = await EnsureCompletedVisitAsync(ahmed, demoDoctorId, UtcAt(now, -21, 9, 0), "Recurring headaches", "Neurological exam normal. Advised hydration and rest.", "Tension headache", recordedBy, now, cancellationToken);
        var monaVisit = await EnsureCompletedVisitAsync(mona, secondDoctorId, UtcAt(now, -14, 9, 30), "Child fever", "Viral illness. Supportive care advised.", "Viral upper respiratory infection", recordedBy, now, cancellationToken);
        var khaledVisit = await EnsureCompletedVisitAsync(khaled, thirdDoctorId, UtcAt(now, -10, 11, 0), "Eczema flare", "Topical steroids prescribed.", "Atopic dermatitis", recordedBy, now, cancellationToken);
        var tarekVisit = await EnsureCompletedVisitAsync(tarek, demoDoctorId, UtcAt(now, -7, 10, 0), "Hypertension review", "Blood pressure improving on current medication.", "Essential hypertension", recordedBy, now, cancellationToken);
        var mostafaVisit = await EnsureCompletedVisitAsync(mostafa, fourthDoctorId, UtcAt(now, -3, 9, 0), "Joint stiffness", "Osteoarthritis changes on examination.", "Osteoarthritis", recordedBy, now, cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        await EnsurePrescriptionAsync(ahmed, demoDoctorId, ahmedVisit, "Take with food.", now, cancellationToken,
            ("Paracetamol", "500 mg", "Oral", 3, 5, "For pain as needed"));
        await EnsurePrescriptionAsync(mona, secondDoctorId, monaVisit, null, now, cancellationToken,
            ("Paracetamol syrup", "120 mg/5 ml", "Oral", 4, 3, "For fever"),
            ("Saline nasal drops", "1 drop", "Nasal", 3, 5, null));
        await EnsurePrescriptionAsync(tarek, demoDoctorId, tarekVisit, "Review in four weeks.", now, cancellationToken,
            ("Amlodipine", "5 mg", "Oral", 1, 30, "Once daily in the morning"));

        await EnsureInvoiceAsync("INV-DEMO-1001", ahmed, ahmedVisit, InvoiceStatus.Paid, 300m, billingUser, now, cancellationToken);
        await EnsureInvoiceAsync("INV-DEMO-1002", mona, monaVisit, InvoiceStatus.PartiallyPaid, 450m, billingUser, now, cancellationToken);
        await EnsureInvoiceAsync("INV-DEMO-1003", khaled, khaledVisit, InvoiceStatus.Issued, 350m, billingUser, now, cancellationToken);
        await EnsureInvoiceAsync("INV-DEMO-1004", tarek, tarekVisit, InvoiceStatus.Void, 300m, billingUser, now, cancellationToken);
        await EnsureInvoiceAsync("INV-DEMO-1005", mostafa, mostafaVisit, InvoiceStatus.Draft, 400m, billingUser, now, cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Seeded rich development demo data (doctors, patients, appointments, visits, prescriptions, invoices).");
    }

    private static DateTime UtcAt(DateTime now, int daysFromToday, int hour, int minute) =>
        DateTime.SpecifyKind(now.Date.AddDays(daysFromToday).AddHours(hour).AddMinutes(minute), DateTimeKind.Utc);

    private async Task<Guid> EnsureUserAsync(string email, string fullName, string role, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            var password = DemoAccounts.DoctorDefaultPassword;
            user = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                FullName = fullName,
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow
            };

            var created = await userManager.CreateAsync(user, password);
            if (!created.Succeeded)
            {
                logger.LogError(
                    "Could not seed demo user {Email}: {Errors}",
                    email,
                    string.Join("; ", created.Errors.Select(error => error.Description)));
                return Guid.Empty;
            }
        }

        if (!await userManager.IsInRoleAsync(user, role))
        {
            await userManager.AddToRoleAsync(user, role);
        }

        return user.Id;
    }

    private async Task<Guid> ResolveUserIdAsync(string email, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByEmailAsync(email);
        return user?.Id ?? Guid.Empty;
    }

    private async Task EnsureScheduleAsync(Guid doctorId, DateTime now, CancellationToken cancellationToken)
    {
        foreach (var day in new[] { DayOfWeek.Sunday, DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday })
        {
            await EnsureScheduleWindowAsync(doctorId, day, MorningStart, MorningEnd, now, cancellationToken);
            await EnsureScheduleWindowAsync(doctorId, day, AfternoonStart, AfternoonEnd, now, cancellationToken);
        }
    }

    private async Task EnsureScheduleWindowAsync(
        Guid doctorId,
        DayOfWeek day,
        TimeOnly start,
        TimeOnly end,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var exists = await dbContext.DoctorSchedules.AnyAsync(
            schedule => schedule.DoctorId == doctorId && schedule.DayOfWeek == day && schedule.StartsAt == start,
            cancellationToken);

        if (!exists)
        {
            dbContext.DoctorSchedules.Add(DoctorSchedule.Create(doctorId, day, start, end, SlotMinutes, now));
        }
    }

    private async Task<Guid> EnsurePatientAsync(
        string mrn,
        string firstName,
        string lastName,
        DateOnly dateOfBirth,
        Gender gender,
        BloodType bloodType,
        string phone,
        string nationalId,
        string? notes,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var existing = await dbContext.Patients
            .Where(patient => patient.MedicalRecordNumber == mrn)
            .Select(patient => patient.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (existing != Guid.Empty)
        {
            return existing;
        }

        var patient = Patient.Create(mrn, firstName, lastName, now);
        patient.UpdateContactDetails(nationalId, phone, null, null, now);
        patient.UpdateDemographics(dateOfBirth, gender, bloodType, now);

        if (!string.IsNullOrWhiteSpace(notes))
        {
            patient.UpdateNotes(notes, now);
        }

        dbContext.Patients.Add(patient);
        await dbContext.SaveChangesAsync(cancellationToken);
        return patient.Id;
    }

    private async Task EnsureAllergyAsync(
        Guid patientId,
        string substance,
        string reaction,
        AllergySeverity severity,
        Guid recordedByUserId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var normalized = SubstanceNormalizer.Normalize(substance);
        var exists = await dbContext.Allergies.AnyAsync(
            allergy => allergy.PatientId == patientId && allergy.NormalizedSubstance == normalized,
            cancellationToken);

        if (!exists)
        {
            dbContext.Allergies.Add(Allergy.Record(patientId, substance, reaction, severity, null, recordedByUserId, now));
        }
    }

    private async Task EnsureHistoryAsync(
        Guid patientId,
        string condition,
        DateOnly diagnosedOn,
        Guid recordedByUserId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var normalized = SubstanceNormalizer.Normalize(condition);
        var exists = await dbContext.MedicalHistoryEntries.AnyAsync(
            entry => entry.PatientId == patientId && entry.NormalizedCondition == normalized,
            cancellationToken);

        if (!exists)
        {
            dbContext.MedicalHistoryEntries.Add(
                MedicalHistoryEntry.Record(patientId, condition, diagnosedOn, null, recordedByUserId, now));
        }
    }

    private async Task EnsureBlockingAsync(
        Guid patientId,
        Guid doctorId,
        DateTime startsAtUtc,
        int durationMinutes,
        string reason,
        bool confirmed,
        Guid bookedByUserId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var slotKey = Appointment.ComputeSlotKey(doctorId, startsAtUtc);
        var exists = await dbContext.Appointments.AnyAsync(
            appointment => appointment.DoctorId == doctorId && appointment.SlotKey == slotKey,
            cancellationToken);

        if (exists)
        {
            return;
        }

        var appointment = Appointment.Book(
            patientId,
            doctorId,
            startsAtUtc,
            startsAtUtc.AddMinutes(durationMinutes),
            reason,
            bookedByUserId,
            now);

        if (confirmed)
        {
            appointment.Confirm(now);
        }

        dbContext.Appointments.Add(appointment);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureTransitionedAsync(
        Guid patientId,
        Guid doctorId,
        DateTime startsAtUtc,
        int durationMinutes,
        string reason,
        AppointmentStatus target,
        string? cancellationReason,
        Guid bookedByUserId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var exists = await dbContext.Appointments.AnyAsync(
            appointment => appointment.PatientId == patientId &&
                           appointment.DoctorId == doctorId &&
                           appointment.StartsAtUtc == startsAtUtc,
            cancellationToken);

        if (exists)
        {
            return;
        }

        var appointment = Appointment.Book(
            patientId,
            doctorId,
            startsAtUtc,
            startsAtUtc.AddMinutes(durationMinutes),
            reason,
            bookedByUserId,
            now);

        switch (target)
        {
            case AppointmentStatus.Cancelled:
                appointment.Cancel(cancellationReason, now);
                break;
            case AppointmentStatus.NoShow:
                appointment.MarkNoShow(now);
                break;
            default:
                break;
        }

        dbContext.Appointments.Add(appointment);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<Guid> EnsureCompletedVisitAsync(
        Guid patientId,
        Guid doctorId,
        DateTime visitAtUtc,
        string chiefComplaint,
        string notes,
        string diagnosis,
        Guid recordedByUserId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var existingVisit = await dbContext.PatientVisits
            .Where(visit => visit.PatientId == patientId && visit.DoctorId == doctorId && visit.VisitAtUtc == visitAtUtc)
            .Select(visit => visit.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (existingVisit != Guid.Empty)
        {
            return existingVisit;
        }

        var appointment = Appointment.Book(
            patientId,
            doctorId,
            visitAtUtc,
            visitAtUtc.AddMinutes(30),
            chiefComplaint,
            recordedByUserId,
            now);
        appointment.Complete(now);
        dbContext.Appointments.Add(appointment);
        await dbContext.SaveChangesAsync(cancellationToken);

        var visit = PatientVisit.Record(
            patientId,
            doctorId,
            appointment.Id,
            visitAtUtc,
            chiefComplaint,
            notes,
            diagnosis,
            recordedByUserId,
            now);

        dbContext.PatientVisits.Add(visit);
        await dbContext.SaveChangesAsync(cancellationToken);
        return visit.Id;
    }

    private async Task EnsurePrescriptionAsync(
        Guid patientId,
        Guid doctorId,
        Guid visitId,
        string? notes,
        DateTime now,
        CancellationToken cancellationToken,
        params (string Drug, string Dose, string? Route, int TimesPerDay, int DurationDays, string? Instructions)[] items)
    {
        var exists = await dbContext.Prescriptions.AnyAsync(
            prescription => prescription.VisitId == visitId,
            cancellationToken);

        if (exists || items.Length == 0)
        {
            return;
        }

        var prescription = Prescription.Create(patientId, doctorId, visitId, notes, now);
        dbContext.Prescriptions.Add(prescription);

        // The prescription id is generated by the database, so it must be persisted before its items
        // can reference it.
        await dbContext.SaveChangesAsync(cancellationToken);

        foreach (var item in items)
        {
            prescription.AddItem(
                item.Drug,
                item.Dose,
                item.Route,
                item.TimesPerDay,
                item.DurationDays,
                item.Instructions,
                doseTimes: null,
                AllergyCheckOutcome.NoKnownAllergy,
                matchedAllergyId: null,
                now);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureInvoiceAsync(
        string invoiceNumber,
        Guid patientId,
        Guid visitId,
        InvoiceStatus target,
        decimal total,
        Guid createdByUserId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var exists = await dbContext.Invoices.AnyAsync(
            invoice => invoice.InvoiceNumber == invoiceNumber,
            cancellationToken);

        if (exists)
        {
            return;
        }

        var consultation = Math.Round(total * 0.7m, 2);
        var procedures = Math.Round(total - consultation, 2);

        var lines = new List<InvoiceLineInput>
        {
            new("Consultation", consultation, 1)
        };

        if (procedures > 0)
        {
            lines.Add(new InvoiceLineInput("Procedures and consumables", procedures, 1));
        }

        var invoice = Invoice.Create(invoiceNumber, patientId, visitId, Currency, lines, createdByUserId, now);

        switch (target)
        {
            case InvoiceStatus.Draft:
                break;
            case InvoiceStatus.Issued:
                invoice.Issue(now);
                break;
            case InvoiceStatus.PartiallyPaid:
                invoice.Issue(now);
                invoice.RecordPayment(Math.Round(total / 2, 2), PaymentMethod.Cash, "PARTIAL-DEMO", createdByUserId, now);
                break;
            case InvoiceStatus.Paid:
                invoice.Issue(now);
                invoice.RecordPayment(total, PaymentMethod.Card, "PAID-DEMO", createdByUserId, now);
                break;
            case InvoiceStatus.Void:
                invoice.Issue(now);
                invoice.Void("Issued in error by the demo seeder.", now);
                break;
            default:
                break;
        }

        dbContext.Invoices.Add(invoice);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
