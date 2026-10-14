using ClinicManagement.Domain.Auditing;
using ClinicManagement.Domain.Prescriptions;
using ClinicManagement.Domain.Privacy;
using ClinicManagement.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClinicManagement.Infrastructure.Persistence.Configurations;

public sealed class PrescriptionConfiguration : IEntityTypeConfiguration<Prescription>
{
    public void Configure(EntityTypeBuilder<Prescription> builder)
    {
        builder.ToTable("Prescriptions");
        builder.HasKey(prescription => prescription.Id);

        builder.Property(prescription => prescription.IssuedAtUtc).IsRequired();
        builder.Property(prescription => prescription.Notes).HasMaxLength(2048);

        builder.HasIndex(prescription => new { prescription.PatientId, prescription.IssuedAtUtc })
            .HasDatabaseName("IX_Prescriptions_Patient_IssuedAt");

        builder.HasIndex(prescription => prescription.DoctorId).HasDatabaseName("IX_Prescriptions_Doctor");

        builder.HasOne(prescription => prescription.Patient)
            .WithMany(patient => patient.Prescriptions)
            .HasForeignKey(prescription => prescription.PatientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(prescription => prescription.Doctor)
            .WithMany(doctor => doctor.Prescriptions)
            .HasForeignKey(prescription => prescription.DoctorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(prescription => prescription.Visit)
            .WithMany()
            .HasForeignKey(prescription => prescription.VisitId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class PrescriptionItemConfiguration : IEntityTypeConfiguration<PrescriptionItem>
{
    public void Configure(EntityTypeBuilder<PrescriptionItem> builder)
    {
        builder.ToTable("PrescriptionItems");
        builder.HasKey(item => item.Id);

        builder.Property(item => item.DrugName).HasMaxLength(200).IsRequired();
        builder.Property(item => item.NormalizedDrugName).HasMaxLength(200).IsRequired();
        builder.Property(item => item.Dose).HasMaxLength(200).IsRequired();
        builder.Property(item => item.Route).HasMaxLength(100);
        builder.Property(item => item.Instructions).HasMaxLength(1024);
        builder.Property(item => item.CreatedAtUtc).IsRequired();

        var doseTimes = builder.Property(item => item.DoseTimes)
            .HasConversion(new DoseTimeListConverter())
            .Metadata;

        doseTimes.SetColumnType("character varying(200)");
        doseTimes.SetValueComparer(new DoseTimeListComparer());

        builder.HasIndex(item => new { item.PrescriptionId, item.NormalizedDrugName })
            .HasDatabaseName("IX_PrescriptionItems_Prescription_Drug");

        builder.HasOne(item => item.Prescription)
            .WithMany(prescription => prescription.Items)
            .HasForeignKey(item => item.PrescriptionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class MedicationReminderConfiguration : IEntityTypeConfiguration<MedicationReminder>
{
    public void Configure(EntityTypeBuilder<MedicationReminder> builder)
    {
        builder.ToTable("MedicationReminders");
        builder.HasKey(reminder => reminder.Id);

        builder.Property(reminder => reminder.ScheduledForUtc).IsRequired();
        builder.Property(reminder => reminder.FailureReason).HasMaxLength(512);

        builder.HasIndex(reminder => new { reminder.PrescriptionItemId, reminder.ScheduledForUtc })
            .IsUnique()
            .HasDatabaseName("UX_MedicationReminders_Item_ScheduledFor");

        builder.HasIndex(reminder => new { reminder.PatientId, reminder.ScheduledForUtc, reminder.Status })
            .HasDatabaseName("IX_MedicationReminders_Patient_ScheduledFor_Status");

        builder.HasOne(reminder => reminder.Patient)
            .WithMany()
            .HasForeignKey(reminder => reminder.PatientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(reminder => reminder.Prescription)
            .WithMany()
            .HasForeignKey(reminder => reminder.PrescriptionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(reminder => reminder.PrescriptionItem)
            .WithMany()
            .HasForeignKey(reminder => reminder.PrescriptionItemId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ConsentRecordConfiguration : IEntityTypeConfiguration<ConsentRecord>
{
    public void Configure(EntityTypeBuilder<ConsentRecord> builder)
    {
        builder.ToTable("ConsentRecords");
        builder.HasKey(consent => consent.Id);

        builder.Property(consent => consent.PolicyVersion).HasMaxLength(50).IsRequired();
        builder.Property(consent => consent.GrantedAtUtc).IsRequired();

        builder.HasIndex(consent => new { consent.PatientId, consent.Type }).HasDatabaseName("IX_ConsentRecords_Patient_Type");

        builder.HasOne(consent => consent.Patient)
            .WithMany()
            .HasForeignKey(consent => consent.PatientId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class AuditLogEntryConfiguration : IEntityTypeConfiguration<AuditLogEntry>
{
    public void Configure(EntityTypeBuilder<AuditLogEntry> builder)
    {
        builder.ToTable("AuditLogEntries");
        builder.HasKey(entry => entry.Id);

        builder.Property(entry => entry.Id).ValueGeneratedOnAdd();
        builder.Property(entry => entry.OccurredAtUtc).IsRequired();
        builder.Property(entry => entry.EntityName).HasMaxLength(128).IsRequired();
        builder.Property(entry => entry.EntityId).HasMaxLength(64);
        builder.Property(entry => entry.ActorRole).HasMaxLength(32);
        builder.Property(entry => entry.CorrelationId).HasMaxLength(64);
        builder.Property(entry => entry.ChangedFields).HasMaxLength(2048);
        builder.Property(entry => entry.EntryHash).HasMaxLength(128).IsRequired();
        builder.Property(entry => entry.PreviousHash).HasMaxLength(128);

        builder.HasIndex(entry => new { entry.OccurredAtUtc }).HasDatabaseName("IX_AuditLogEntries_OccurredAt");
        builder.HasIndex(entry => new { entry.ActorUserId, entry.OccurredAtUtc }).HasDatabaseName("IX_AuditLogEntries_Actor_OccurredAt");
        builder.HasIndex(entry => new { entry.PatientId, entry.OccurredAtUtc }).HasDatabaseName("IX_AuditLogEntries_Patient_OccurredAt");
    }
}

public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("RefreshTokens");
        builder.HasKey(token => token.Id);

        builder.Property(token => token.TokenHash).HasMaxLength(128).IsRequired();
        builder.Property(token => token.SecurityStamp).HasMaxLength(128).IsRequired();
        builder.Property(token => token.CreatedAtUtc).IsRequired();
        builder.Property(token => token.ExpiresAtUtc).IsRequired();

        builder.HasIndex(token => token.TokenHash).IsUnique().HasDatabaseName("UX_RefreshTokens_TokenHash");
        builder.HasIndex(token => new { token.ApplicationUserId, token.ExpiresAtUtc }).HasDatabaseName("IX_RefreshTokens_User_ExpiresAt");

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(token => token.ApplicationUserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}