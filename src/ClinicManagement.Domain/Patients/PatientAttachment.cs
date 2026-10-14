using ClinicManagement.Domain.Common;

namespace ClinicManagement.Domain.Patients;

public sealed class PatientAttachment
{
    private PatientAttachment()
    {
    }

    public Guid Id { get; private set; }

    public Guid PatientId { get; private set; }

    public Patient? Patient { get; private set; }

    public Guid? VisitId { get; private set; }

    public PatientVisit? Visit { get; private set; }

    public string FileName { get; private set; } = string.Empty;

    public string ContentType { get; private set; } = string.Empty;

    public long SizeBytes { get; private set; }

    public string StorageKey { get; private set; } = string.Empty;

    public string? Sha256 { get; private set; }

    public Guid UploadedByUserId { get; private set; }

    public DateTime UploadedAtUtc { get; private set; }

    public static PatientAttachment Register(
        Guid patientId,
        Guid? visitId,
        string fileName,
        string contentType,
        long sizeBytes,
        string storageKey,
        string? sha256,
        Guid uploadedByUserId,
        DateTime nowUtc)
    {
        if (patientId == Guid.Empty)
        {
            throw new DomainException(ErrorCodes.ValidationFailed, "patientId is required.");
        }

        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new DomainException(ErrorCodes.ValidationFailed, "fileName is required.");
        }

        if (string.IsNullOrWhiteSpace(storageKey))
        {
            throw new DomainException(ErrorCodes.ValidationFailed, "storageKey is required.");
        }

        if (sizeBytes <= 0)
        {
            throw new DomainException(ErrorCodes.ValidationFailed, "sizeBytes must be greater than zero.");
        }

        return new PatientAttachment
        {
            PatientId = patientId,
            VisitId = visitId,
            FileName = fileName.Trim(),
            ContentType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType.Trim(),
            SizeBytes = sizeBytes,
            StorageKey = storageKey.Trim(),
            Sha256 = sha256,
            UploadedByUserId = uploadedByUserId,
            UploadedAtUtc = nowUtc
        };
    }
}