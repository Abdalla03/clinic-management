using ClinicManagement.Domain.Appointments;
using ClinicManagement.Domain.Patients;
using ClinicManagement.Domain.Users;
using ClinicManagement.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClinicManagement.Infrastructure.Persistence.Configurations;

public sealed class DoctorConfiguration : IEntityTypeConfiguration<Doctor>
{
    public void Configure(EntityTypeBuilder<Doctor> builder)
    {
        builder.ToTable("Doctors");
        builder.HasKey(doctor => doctor.Id);

        builder.Property(doctor => doctor.Specialty).HasMaxLength(200).IsRequired();
        builder.Property(doctor => doctor.LicenseNumber).HasMaxLength(64).IsRequired();
        builder.Property(doctor => doctor.CreatedAtUtc).IsRequired();

        builder.HasIndex(doctor => doctor.LicenseNumber).IsUnique();
        builder.HasIndex(doctor => doctor.ApplicationUserId).IsUnique();

        builder.HasOne<ApplicationUser>()
            .WithOne(user => user.Doctor)
            .HasForeignKey<Doctor>(doctor => doctor.ApplicationUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class PatientConfiguration : IEntityTypeConfiguration<Patient>
{
    public void Configure(EntityTypeBuilder<Patient> builder)
    {
        builder.ToTable("Patients");
        builder.HasKey(patient => patient.Id);

        builder.Property(patient => patient.MedicalRecordNumber).HasMaxLength(32).IsRequired();
        builder.Property(patient => patient.FirstName).HasMaxLength(100).IsRequired();
        builder.Property(patient => patient.LastName).HasMaxLength(100).IsRequired();
        builder.Property(patient => patient.NationalId).HasMaxLength(256);
        builder.Property(patient => patient.PhoneNumber).HasMaxLength(64);
        builder.Property(patient => patient.EmergencyContactPhone).HasMaxLength(64);
        builder.Property(patient => patient.EmergencyContactName).HasMaxLength(200);
        builder.Property(patient => patient.Address).HasMaxLength(512);
        builder.Property(patient => patient.Notes).HasMaxLength(1024);
        builder.Property(patient => patient.CreatedAtUtc).IsRequired();
        builder.Property(patient => patient.UpdatedAtUtc).IsRequired();

        builder.Property(patient => patient.DateOfBirth).HasColumnName("DateOfBirth");

        builder.Property(patient => patient.Version).IsConcurrencyToken();

        builder.HasIndex(patient => patient.MedicalRecordNumber).IsUnique();
        builder.HasIndex(patient => patient.LastName).HasDatabaseName("IX_Patients_LastName");
        builder.HasIndex(patient => patient.ApplicationUserId).IsUnique();

        builder.HasOne<ApplicationUser>()
            .WithOne(user => user.Patient)
            .HasForeignKey<Patient>(patient => patient.ApplicationUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class AllergyConfiguration : IEntityTypeConfiguration<Allergy>
{
    public void Configure(EntityTypeBuilder<Allergy> builder)
    {
        builder.ToTable("Allergies");
        builder.HasKey(allergy => allergy.Id);

        builder.Property(allergy => allergy.SubstanceName).HasMaxLength(200).IsRequired();
        builder.Property(allergy => allergy.NormalizedSubstance).HasMaxLength(200).IsRequired();
        builder.Property(allergy => allergy.Reaction).HasMaxLength(200);
        builder.Property(allergy => allergy.Notes).HasMaxLength(1024);
        builder.Property(allergy => allergy.RecordedAtUtc).IsRequired();

        builder.HasIndex(allergy => new { allergy.PatientId, allergy.NormalizedSubstance })
            .IsUnique()
            .HasDatabaseName("UX_Allergies_Patient_NormalizedSubstance");

        builder.HasIndex(allergy => allergy.NormalizedSubstance).HasDatabaseName("IX_Allergies_NormalizedSubstance");

        builder.HasOne(allergy => allergy.Patient)
            .WithMany(patient => patient.Allergies)
            .HasForeignKey(allergy => allergy.PatientId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class MedicalHistoryEntryConfiguration : IEntityTypeConfiguration<MedicalHistoryEntry>
{
    public void Configure(EntityTypeBuilder<MedicalHistoryEntry> builder)
    {
        builder.ToTable("MedicalHistoryEntries");
        builder.HasKey(entry => entry.Id);

        builder.Property(entry => entry.ConditionName).HasMaxLength(200).IsRequired();
        builder.Property(entry => entry.NormalizedCondition).HasMaxLength(200).IsRequired();
        builder.Property(entry => entry.Notes).HasMaxLength(2048);
        builder.Property(entry => entry.RecordedAtUtc).IsRequired();

        builder.HasIndex(entry => new { entry.PatientId, entry.NormalizedCondition }).HasDatabaseName("IX_MedicalHistory_Patient_Condition");

        builder.HasOne(entry => entry.Patient)
            .WithMany(patient => patient.MedicalHistory)
            .HasForeignKey(entry => entry.PatientId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class PatientVisitConfiguration : IEntityTypeConfiguration<PatientVisit>
{
    public void Configure(EntityTypeBuilder<PatientVisit> builder)
    {
        builder.ToTable("PatientVisits");
        builder.HasKey(visit => visit.Id);

        builder.Property(visit => visit.ChiefComplaint).HasMaxLength(2048);
        builder.Property(visit => visit.Notes).HasMaxLength(8192);
        builder.Property(visit => visit.Diagnosis).HasMaxLength(2048);
        builder.Property(visit => visit.VisitAtUtc).IsRequired();
        builder.Property(visit => visit.CreatedAtUtc).IsRequired();

        builder.HasIndex(visit => new { visit.PatientId, visit.VisitAtUtc }).HasDatabaseName("IX_PatientVisits_Patient_VisitAt");
        builder.HasIndex(visit => visit.DoctorId).HasDatabaseName("IX_PatientVisits_Doctor");

        builder.HasOne(visit => visit.Patient)
            .WithMany(patient => patient.Visits)
            .HasForeignKey(visit => visit.PatientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(visit => visit.Doctor)
            .WithMany(doctor => doctor.Visits)
            .HasForeignKey(visit => visit.DoctorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(visit => visit.Appointment)
            .WithMany()
            .HasForeignKey(visit => visit.AppointmentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class PatientAttachmentConfiguration : IEntityTypeConfiguration<PatientAttachment>
{
    public void Configure(EntityTypeBuilder<PatientAttachment> builder)
    {
        builder.ToTable("PatientAttachments");
        builder.HasKey(attachment => attachment.Id);

        builder.Property(attachment => attachment.FileName).HasMaxLength(255).IsRequired();
        builder.Property(attachment => attachment.ContentType).HasMaxLength(150).IsRequired();
        builder.Property(attachment => attachment.StorageKey).HasMaxLength(512).IsRequired();
        builder.Property(attachment => attachment.Sha256).HasMaxLength(64);
        builder.Property(attachment => attachment.UploadedAtUtc).IsRequired();

        builder.HasIndex(attachment => attachment.PatientId).HasDatabaseName("IX_PatientAttachments_Patient");

        builder.HasOne(attachment => attachment.Patient)
            .WithMany(patient => patient.Attachments)
            .HasForeignKey(attachment => attachment.PatientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(attachment => attachment.Visit)
            .WithMany()
            .HasForeignKey(attachment => attachment.VisitId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class AppointmentConfiguration : IEntityTypeConfiguration<Appointment>
{
    public void Configure(EntityTypeBuilder<Appointment> builder)
    {
        builder.ToTable("Appointments");
        builder.HasKey(appointment => appointment.Id);

        builder.Property(appointment => appointment.ReasonForVisit).HasMaxLength(512);
        builder.Property(appointment => appointment.CancellationReason).HasMaxLength(512);
        builder.Property(appointment => appointment.StartsAtUtc).IsRequired();
        builder.Property(appointment => appointment.EndsAtUtc).IsRequired();
        builder.Property(appointment => appointment.CreatedAtUtc).IsRequired();
        builder.Property(appointment => appointment.UpdatedAtUtc).IsRequired();
        builder.Property(appointment => appointment.Version).IsConcurrencyToken();

        builder.HasIndex(appointment => new { appointment.DoctorId, appointment.SlotKey })
            .IsUnique()
            .HasDatabaseName("UX_Appointments_Doctor_SlotKey");

        builder.HasIndex(appointment => new { appointment.DoctorId, appointment.StartsAtUtc })
            .HasDatabaseName("IX_Appointments_Doctor_StartsAt");

        builder.HasIndex(appointment => new { appointment.PatientId, appointment.StartsAtUtc })
            .HasDatabaseName("IX_Appointments_Patient_StartsAt");

        builder.HasOne(appointment => appointment.Patient)
            .WithMany(patient => patient.Appointments)
            .HasForeignKey(appointment => appointment.PatientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(appointment => appointment.Doctor)
            .WithMany(doctor => doctor.Appointments)
            .HasForeignKey(appointment => appointment.DoctorId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class DoctorScheduleConfiguration : IEntityTypeConfiguration<DoctorSchedule>
{
    public void Configure(EntityTypeBuilder<DoctorSchedule> builder)
    {
        builder.ToTable("DoctorSchedules");
        builder.HasKey(schedule => schedule.Id);

        builder.Property(schedule => schedule.CreatedAtUtc).IsRequired();

        builder.HasIndex(schedule => new { schedule.DoctorId, schedule.DayOfWeek, schedule.StartsAt })
            .IsUnique()
            .HasDatabaseName("UX_DoctorSchedules_Doctor_Day_Start");

        builder.HasOne(schedule => schedule.Doctor)
            .WithMany(doctor => doctor.Schedules)
            .HasForeignKey(schedule => schedule.DoctorId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}