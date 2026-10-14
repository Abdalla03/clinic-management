namespace ClinicManagement.Domain.Privacy;

public enum ConsentType
{
    DataProcessing = 1,
    AiAssistance = 2,
    DataExport = 3
}

public enum ConsentMethod
{
    Written = 1,
    Verbal = 2,
    System = 3
}

public sealed class ConsentRecord
{
    private ConsentRecord()
    {
    }

    public Guid Id { get; private set; }

    public Guid PatientId { get; private set; }

    public Patients.Patient? Patient { get; private set; }

    public ConsentType Type { get; private set; }

    public string PolicyVersion { get; private set; } = string.Empty;

    public ConsentMethod Method { get; private set; }

    public DateTime GrantedAtUtc { get; private set; }

    public Guid? GrantedByUserId { get; private set; }

    public DateTime? RevokedAtUtc { get; private set; }

    public Guid? RevokedByUserId { get; private set; }

    public bool IsActive => RevokedAtUtc is null;

    public static ConsentRecord Grant(
        Guid patientId,
        ConsentType type,
        string policyVersion,
        ConsentMethod method,
        Guid? grantedByUserId,
        DateTime nowUtc)
    {
        if (patientId == Guid.Empty)
        {
            throw new Common.DomainException(Common.ErrorCodes.ValidationFailed, "patientId is required.");
        }

        if (string.IsNullOrWhiteSpace(policyVersion))
        {
            throw new Common.DomainException(Common.ErrorCodes.ValidationFailed, "policyVersion is required.");
        }

        return new ConsentRecord
        {
            PatientId = patientId,
            Type = type,
            PolicyVersion = policyVersion.Trim(),
            Method = method,
            GrantedAtUtc = nowUtc,
            GrantedByUserId = grantedByUserId
        };
    }

    public void Revoke(Guid revokedByUserId, DateTime nowUtc)
    {
        if (!IsActive)
        {
            throw new Common.DomainException(Common.ErrorCodes.InvalidStateTransition, "Consent is already revoked.");
        }

        RevokedAtUtc = nowUtc;
        RevokedByUserId = revokedByUserId;
    }
}