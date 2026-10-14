using System.Reflection;
using ClinicManagement.Application.Abstractions;
using ClinicManagement.Domain.Appointments;
using ClinicManagement.Domain.Auditing;
using ClinicManagement.Domain.Billing;
using ClinicManagement.Domain.Common;
using ClinicManagement.Domain.Patients;
using ClinicManagement.Domain.Privacy;
using ClinicManagement.Domain.Prescriptions;
using ClinicManagement.Domain.Users;
using ClinicManagement.Infrastructure.Identity;
using ClinicManagement.Infrastructure.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace ClinicManagement.Infrastructure.Persistence;

public sealed class ClinicDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
{
    private readonly ISecretProtector _fieldProtector;

    /// <summary>
    /// The field protector is required rather than optional on purpose: the model must be identical
    /// in every process that touches it, otherwise EF's model cache would hand one host the model
    /// built by another and encryption would silently be on or off depending on who got there first.
    /// </summary>
    public ClinicDbContext(DbContextOptions<ClinicDbContext> options, ISecretProtector fieldProtector)
        : base(options)
    {
        ArgumentNullException.ThrowIfNull(fieldProtector);
        _fieldProtector = fieldProtector;
    }

    public DbSet<Doctor> Doctors => Set<Doctor>();

    public DbSet<Patient> Patients => Set<Patient>();

    public DbSet<Allergy> Allergies => Set<Allergy>();

    public DbSet<MedicalHistoryEntry> MedicalHistoryEntries => Set<MedicalHistoryEntry>();

    public DbSet<PatientVisit> PatientVisits => Set<PatientVisit>();

    public DbSet<PatientAttachment> PatientAttachments => Set<PatientAttachment>();

    public DbSet<Appointment> Appointments => Set<Appointment>();

    public DbSet<DoctorSchedule> DoctorSchedules => Set<DoctorSchedule>();

    public DbSet<Prescription> Prescriptions => Set<Prescription>();

    public DbSet<PrescriptionItem> PrescriptionItems => Set<PrescriptionItem>();

    public DbSet<MedicationReminder> MedicationReminders => Set<MedicationReminder>();

    public DbSet<Invoice> Invoices => Set<Invoice>();

    public DbSet<InvoiceLine> InvoiceLines => Set<InvoiceLine>();

    public DbSet<Payment> Payments => Set<Payment>();

    public DbSet<ConsentRecord> ConsentRecords => Set<ConsentRecord>();

    public DbSet<AuditLogEntry> AuditLogEntries => Set<AuditLogEntry>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        builder.Entity<ApplicationUser>().ToTable("Users");
        builder.Entity<IdentityRole<Guid>>().ToTable("Roles");
        builder.Entity<IdentityUserRole<Guid>>().ToTable("UserRoles");
        builder.Entity<IdentityUserClaim<Guid>>().ToTable("UserClaims");
        builder.Entity<IdentityRoleClaim<Guid>>().ToTable("RoleClaims");
        builder.Entity<IdentityUserLogin<Guid>>().ToTable("UserLogins");
        builder.Entity<IdentityUserToken<Guid>>().ToTable("UserTokens");

        ApplyEnumStringConversions(builder);
        ApplyFieldEncryption(builder);
    }

    /// <summary>
    /// Turns every <c>[EncryptedColumn]</c> string property into a column that stores ciphertext.
    /// </summary>
    private void ApplyFieldEncryption(ModelBuilder builder)
    {
        foreach (var property in builder.Model
                     .GetEntityTypes()
                     .SelectMany(entityType => entityType.GetProperties())
                     .Where(property =>
                         property.ClrType == typeof(string) &&
                         property.PropertyInfo?.GetCustomAttribute<EncryptedColumnAttribute>() is not null))
        {
            property.SetMaxLength(null);
            property.SetValueConverter(new EncryptedStringConverter(
                _fieldProtector,
                AesGcmSecretProtector.ClinicalFieldPurpose));
        }
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        NormalizeUtcDateTimes();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        NormalizeUtcDateTimes();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void NormalizeUtcDateTimes()
    {
        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified))
            {
                continue;
            }

            foreach (var property in entry.Properties)
            {
                if (property.Metadata.ClrType != typeof(DateTime) && property.Metadata.ClrType != typeof(DateTime?))
                {
                    continue;
                }

                if (property.CurrentValue is not DateTime value)
                {
                    continue;
                }

                property.CurrentValue = value.Kind switch
                {
                    DateTimeKind.Utc => value,
                    DateTimeKind.Local => value.ToUniversalTime(),
                    _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
                };
            }
        }
    }

    private static void ApplyEnumStringConversions(ModelBuilder builder)
    {
        var enumProperties = builder.Model
            .GetEntityTypes()
            .SelectMany(entityType => entityType.GetProperties())
            .Where(property => IsEnum(property.ClrType))
            .ToList();

        foreach (var property in enumProperties)
        {
            var underlyingType = Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType;
            var converterType = typeof(EnumToStringConverter<>).MakeGenericType(underlyingType);
            var converter = (ValueConverter)Activator.CreateInstance(converterType)!;
            property.SetValueConverter(converter);
            property.SetMaxLength(32);
        }

        static bool IsEnum(Type type) =>
            type.IsEnum || (Nullable.GetUnderlyingType(type)?.IsEnum ?? false);
    }
}