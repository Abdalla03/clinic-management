using ClinicManagement.Domain.Common;

namespace ClinicManagement.Domain.Prescriptions;

public sealed class PrescriptionItem
{
    private readonly List<DoseTime> _doseTimes = [];

    private PrescriptionItem()
    {
    }

    public Guid Id { get; private set; }

    public Guid PrescriptionId { get; private set; }

    public Prescription? Prescription { get; private set; }

    public string DrugName { get; private set; } = string.Empty;

    public string NormalizedDrugName { get; private set; } = string.Empty;

    [EncryptedColumn]
    public string Dose { get; private set; } = string.Empty;

    public string? Route { get; private set; }

    public int TimesPerDay { get; private set; }

    public int DurationDays { get; private set; }

    [EncryptedColumn]
    public string? Instructions { get; private set; }

    public IReadOnlyCollection<DoseTime> DoseTimes => _doseTimes;

    public AllergyCheckOutcome AllergyCheckOutcome { get; private set; }

    public Guid? MatchedAllergyId { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public static PrescriptionItem Create(
        Guid prescriptionId,
        string drugName,
        string dose,
        string? route,
        int timesPerDay,
        int durationDays,
        string? instructions,
        IEnumerable<DoseTime>? doseTimes,
        AllergyCheckOutcome allergyCheckOutcome,
        Guid? matchedAllergyId,
        DateTime nowUtc)
    {
        if (prescriptionId == Guid.Empty)
        {
            throw new DomainException(ErrorCodes.ValidationFailed, "prescriptionId is required.");
        }

        var name = drugName?.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException(ErrorCodes.ValidationFailed, "drugName is required.");
        }

        if (string.IsNullOrWhiteSpace(dose))
        {
            throw new DomainException(ErrorCodes.ValidationFailed, "dose is required.");
        }

        if (timesPerDay is < 1 or > 12)
        {
            throw new DomainException(ErrorCodes.ValidationFailed, "timesPerDay must be between 1 and 12.");
        }

        if (durationDays is < 1 or > 365)
        {
            throw new DomainException(ErrorCodes.ValidationFailed, "durationDays must be between 1 and 365.");
        }

        var times = (doseTimes ?? []).Distinct().OrderBy(t => t.Utc).ToList();
        if (times.Count > 0 && times.Count != timesPerDay)
        {
            throw new DomainException(
                ErrorCodes.ValidationFailed,
                "doseTimes count must equal timesPerDay when dose times are supplied.");
        }

        if (allergyCheckOutcome == AllergyCheckOutcome.MatchedAllergy && matchedAllergyId is null)
        {
            throw new DomainException(ErrorCodes.ValidationFailed, "matchedAllergyId is required when the allergy check matched.");
        }

        var item = new PrescriptionItem
        {
            PrescriptionId = prescriptionId,
            DrugName = name,
            NormalizedDrugName = SubstanceNormalizer.Normalize(name),
            Dose = dose.Trim(),
            Route = string.IsNullOrWhiteSpace(route) ? null : route.Trim(),
            TimesPerDay = timesPerDay,
            DurationDays = durationDays,
            Instructions = string.IsNullOrWhiteSpace(instructions) ? null : instructions.Trim(),
            AllergyCheckOutcome = allergyCheckOutcome,
            MatchedAllergyId = matchedAllergyId,
            CreatedAtUtc = nowUtc
        };

        item._doseTimes.AddRange(times);
        return item;
    }
}