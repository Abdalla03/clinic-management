using System.Globalization;
using ClinicManagement.Application.Abstractions;
using ClinicManagement.Domain.Prescriptions;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace ClinicManagement.Infrastructure.Persistence;

public sealed class EnumToStringConverter<TEnum> : ValueConverter<TEnum, string>
    where TEnum : struct, Enum
{
    public EnumToStringConverter()
        : base(
            value => value.ToString(),
            value => (TEnum)Enum.Parse(typeof(TEnum), value, ignoreCase: true))
    {
    }
}

public sealed class DoseTimeListConverter : ValueConverter<IReadOnlyCollection<DoseTime>, string>
{
    private const char Separator = ',';
    private const string TimeFormat = "HH\\:mm";

    public DoseTimeListConverter()
        : base(
            times => string.Join(Separator, times.Select(time => time.Utc.ToString(TimeFormat, CultureInfo.InvariantCulture))),
            raw => (IReadOnlyCollection<DoseTime>)Parse(raw))
    {
    }

    private static List<DoseTime> Parse(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return [];
        }

        var results = new List<DoseTime>();
        foreach (var part in raw.Split(Separator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (TimeOnly.TryParse(part, CultureInfo.InvariantCulture, out var parsed))
            {
                results.Add(new DoseTime(parsed));
            }
        }

        return results;
    }
}

// IReadOnlyCollection<DoseTime> is a reference type, so EF cannot tell whether two snapshots point
// at the same instance or at equal sequences. Without this comparer it emits a value-comparer
// warning and skips change detection, meaning dose-time edits would never be persisted.
public sealed class DoseTimeListComparer : ValueComparer<IReadOnlyCollection<DoseTime>>
{
    public DoseTimeListComparer()
        : base(
            (left, right) => left != null && right != null && left.SequenceEqual(right),
            times => times.Aggregate(0, (hash, time) => HashCode.Combine(hash, time)),
            times => times.ToList())
    {
    }
}

// Writes ciphertext and reads plaintext for every property marked [EncryptedColumn]. The domain
// model never sees encrypted values, so they are encrypted exactly once, on the way to the database.
public sealed class EncryptedStringConverter : ValueConverter<string?, string?>
{
    public EncryptedStringConverter(ISecretProtector protector, string purpose)
        : base(
            value => value == null ? null : protector.Protect(value, purpose),
            value => value == null ? null : protector.Unprotect(value, purpose))
    {
    }
}