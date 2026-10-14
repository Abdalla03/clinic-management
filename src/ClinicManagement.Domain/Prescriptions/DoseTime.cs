namespace ClinicManagement.Domain.Prescriptions;

public readonly record struct DoseTime(TimeOnly Utc)
{
    public override string ToString() => Utc.ToString("HH\\:mm");
}