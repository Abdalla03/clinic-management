using System.Reflection;
using ClinicManagement.Domain.Appointments;
using ClinicManagement.Domain.Auditing;
using ClinicManagement.Domain.Common;
using ClinicManagement.Domain.Patients;
using ClinicManagement.Domain.Prescriptions;
using ClinicManagement.Infrastructure.Identity;
using ClinicManagement.Infrastructure.Persistence;
using ClinicManagement.UnitTests.TestDoubles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace ClinicManagement.UnitTests.Persistence;

public sealed class ClinicModelTests
{
    [Fact]
    public void Model_BuildsWithoutAnyEntityFrameworkWarnings()
    {
        // Treat every EF Core diagnostic as a failure while building the model, so a new shadow
        // foreign key, a missing value comparer or any other modelling warning fails here instead
        // of surfacing later as a silent schema defect.
        var options = new DbContextOptionsBuilder<ClinicDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=clinic;Username=clinic;Password=clinic")
            .ConfigureWarnings(warnings => warnings.Default(WarningBehavior.Throw))
            .Options;

        using var context = new ClinicDbContext(options, new MarkerSecretProtector());

        var exception = Record.Exception(() =>
        {
            foreach (var entityType in context.Model.GetEntityTypes())
            {
                _ = entityType.GetProperties().ToArray();
                _ = entityType.GetForeignKeys().ToArray();
                _ = entityType.GetIndexes().ToArray();
            }
        });

        Assert.True(exception is null, $"The EF model produced warnings: {exception}");
    }

    [Fact]
    public void Model_HasNoShadowForeignKeys()
    {
        using var context = CreateContext();

        var shadowForeignKeys = context.Model
            .GetEntityTypes()
            .SelectMany(entityType => entityType.GetForeignKeys())
            .SelectMany(key => key.Properties)
            .Where(property => property.IsShadowProperty())
            .Select(property => $"{property.DeclaringType.DisplayName()}.{property.Name}")
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(shadowForeignKeys);
    }

    [Fact]
    public void Allergy_AndMedicalHistoryEntry_UseThePatientIdForeignKeyOnly()
    {
        using var context = CreateContext();

        foreach (var entity in new[] { typeof(Allergy), typeof(MedicalHistoryEntry) })
        {
            var foreignKey = context.Model.FindEntityType(entity)!
                .GetForeignKeys()
                .Single(key => key.Properties.Any(property => property.Name == "PatientId"));

            Assert.Equal(new[] { "PatientId" }, foreignKey.Properties.Select(property => property.Name).ToArray());
        }
    }

    [Fact]
    public void Appointment_HasAUniqueIndexOnDoctorAndSlotKey()
    {
        using var context = CreateContext();

        var index = context.Model
            .FindEntityType(typeof(Appointment))!
            .GetIndexes()
            .Single(candidate => candidate.GetDatabaseName() == "UX_Appointments_Doctor_SlotKey");

        Assert.True(index.IsUnique);
        Assert.Equal(
            [nameof(Appointment.DoctorId), nameof(Appointment.SlotKey)],
            index.Properties.Select(property => property.Name).ToArray());
    }

    [Fact]
    public void Appointment_SlotKeyIsNullableSoCancelledSlotsAreReusable()
    {
        using var context = CreateContext();

        var property = context.Model.FindEntityType(typeof(Appointment))!.FindProperty(nameof(Appointment.SlotKey))!;

        Assert.False(property.IsNullable == false);
    }

    [Fact]
    public void Patient_MedicalRecordNumberIsUnique()
    {
        using var context = CreateContext();

        var index = context.Model
            .FindEntityType(typeof(Patient))!
            .GetIndexes()
            .Single(candidate => candidate.Properties.Any(property => property.Name == nameof(Patient.MedicalRecordNumber)));

        Assert.True(index.IsUnique);
    }

    [Fact]
    public void Allergy_HasAUniqueIndexOnPatientAndNormalizedSubstance()
    {
        using var context = CreateContext();

        var index = context.Model
            .FindEntityType(typeof(Allergy))!
            .GetIndexes()
            .Single(candidate => candidate.GetDatabaseName() == "UX_Allergies_Patient_NormalizedSubstance");

        Assert.True(index.IsUnique);
    }

    [Fact]
    public void EnumProperties_ArePersistedAsReadableStrings()
    {
        using var context = CreateContext();

        var appointment = context.Model.FindEntityType(typeof(Appointment))!;
        var status = appointment.FindProperty(nameof(Appointment.Status))!;
        var slotKey = appointment.FindProperty(nameof(Appointment.SlotKey))!;

        Assert.Equal("character varying(32)", status.GetColumnType());
        Assert.Equal("bigint", slotKey.GetColumnType());
    }

    [Fact]
    public void PrescriptionItem_DoseTimesArePersistedAsATransparentList()
    {
        using var context = CreateContext();

        var doseTimes = context.Model
            .FindEntityType(typeof(PrescriptionItem))!
            .FindProperty(nameof(PrescriptionItem.DoseTimes))!;

        Assert.NotNull(doseTimes.GetValueConverter());
        Assert.Equal("character varying(200)", doseTimes.GetColumnType());
        Assert.IsType<DoseTimeListComparer>(doseTimes.GetValueComparer());
    }

    [Fact]
    public void AuditLogEntry_PrimaryKeyIsDatabaseGenerated()
    {
        using var context = CreateContext();

        var id = context.Model.FindEntityType(typeof(AuditLogEntry))!.FindProperty(nameof(AuditLogEntry.Id))!;

        Assert.Equal(ValueGenerated.OnAdd, id.ValueGenerated);
    }

    [Fact]
    public void AuditLogEntry_StoresChangedFieldNamesOnly()
    {
        using var context = CreateContext();

        var changedFields = context.Model
            .FindEntityType(typeof(AuditLogEntry))!
            .FindProperty(nameof(AuditLogEntry.ChangedFields))!;

        Assert.NotNull(changedFields);
        Assert.True(changedFields.GetMaxLength() > 0);
    }

    [Fact]
    public void AllClinicalTables_AreMapped()
    {
        using var context = CreateContext();

        var tables = context.Model.GetEntityTypes().Select(entityType => entityType.GetTableName()).ToHashSet(StringComparer.Ordinal);

        Assert.Contains("Users", tables);
        Assert.Contains("Roles", tables);
        Assert.Contains("Doctors", tables);
        Assert.Contains("Patients", tables);
        Assert.Contains("Allergies", tables);
        Assert.Contains("MedicalHistoryEntries", tables);
        Assert.Contains("PatientVisits", tables);
        Assert.Contains("PatientAttachments", tables);
        Assert.Contains("Appointments", tables);
        Assert.Contains("DoctorSchedules", tables);
        Assert.Contains("Prescriptions", tables);
        Assert.Contains("PrescriptionItems", tables);
        Assert.Contains("MedicationReminders", tables);
        Assert.Contains("ConsentRecords", tables);
        Assert.Contains("AuditLogEntries", tables);
        Assert.Contains("RefreshTokens", tables);
    }

    [Fact]
    public void ClinicalForeignKeys_UseRestrictSoRecordsAreNeverOrphaned()
    {
        using var context = CreateContext();

        var appointment = context.Model.FindEntityType(typeof(Appointment))!;

        Assert.Equal(
            DeleteBehavior.Restrict,
            appointment.GetForeignKeys().Single(key => key.Properties.Any(property => property.Name == nameof(Appointment.PatientId))).DeleteBehavior);

        Assert.Equal(
            DeleteBehavior.Restrict,
            appointment.GetForeignKeys().Single(key => key.Properties.Any(property => property.Name == nameof(Appointment.DoctorId))).DeleteBehavior);
    }

    [Fact]
    public void UtcDateTimes_AreMappedToTimezoneAwareColumns()
    {
        using var context = CreateContext();

        var startsAt = context.Model
            .FindEntityType(typeof(Appointment))!
            .FindProperty(nameof(Appointment.StartsAtUtc))!;

        Assert.Equal("timestamp with time zone", startsAt.GetColumnType());
    }

    [Fact]
    public void ApplicationUser_StoresTheTotpSecretInASeparateColumn()
    {
        using var context = CreateContext();

        var totpSecret = context.Model
            .FindEntityType(typeof(ApplicationUser))!
            .FindProperty(nameof(ApplicationUser.TotpSecretCiphertext))!;

        Assert.NotNull(totpSecret);
        Assert.True(totpSecret.GetMaxLength() > 0);
    }

    [Fact]
    public void EncryptedColumns_AreUnboundedSoCiphertextNeverOverflowsThePlaintextBudget()
    {
        using var context = CreateContext();

        foreach (var property in EncryptedPropertiesOf(context))
        {
            Assert.True(
                property.GetMaxLength() is null,
                $"{property.DeclaringType!.DisplayName()}.{property.Name} must not carry a length limit, " +
                $"otherwise a column sized for plaintext can reject the ciphertext.");
        }
    }

    [Fact]
    public void OnlyPropertiesExplicitlyMarkedEncrypted_HaveAValueConverter()
    {
        using var context = CreateContext();

        foreach (var property in EncryptedPropertiesOf(context))
        {
            Assert.NotNull(property.GetValueConverter());
        }

        // AuthService already encrypts the TOTP secret with its own purpose, so the generic
        // [EncryptedColumn] converter must leave it alone.
        var totpSecret = context.Model
            .FindEntityType(typeof(ApplicationUser))!
            .FindProperty(nameof(ApplicationUser.TotpSecretCiphertext))!;

        Assert.Null(totpSecret.GetValueConverter());
    }

    private static IEnumerable<IProperty> EncryptedPropertiesOf(ClinicDbContext context) =>
        context.Model
            .GetEntityTypes()
            .SelectMany(entityType => entityType.GetProperties())
            .Where(property =>
                property.ClrType == typeof(string) &&
                property.PropertyInfo?.GetCustomAttribute<EncryptedColumnAttribute>() is not null);

    private static ClinicDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ClinicDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=clinic;Username=clinic;Password=clinic")
            .Options;

        return new ClinicDbContext(options, new MarkerSecretProtector());
    }
}
