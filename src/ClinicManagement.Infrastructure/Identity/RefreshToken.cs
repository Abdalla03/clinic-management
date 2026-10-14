namespace ClinicManagement.Infrastructure.Identity;

public sealed class RefreshToken
{
    private RefreshToken()
    {
    }

    public Guid Id { get; private set; } = Guid.NewGuid();

    public Guid ApplicationUserId { get; private set; }

    public string TokenHash { get; private set; } = string.Empty;

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime ExpiresAtUtc { get; private set; }

    public DateTime? RevokedAtUtc { get; private set; }

    public Guid? ReplacedByTokenId { get; private set; }

    public string SecurityStamp { get; private set; } = string.Empty;

    public bool IsUsable(DateTime nowUtc) => RevokedAtUtc is null && ExpiresAtUtc > nowUtc;

    public static RefreshToken Create(
        Guid applicationUserId,
        string tokenHash,
        DateTime createdAtUtc,
        DateTime expiresAtUtc,
        string securityStamp)
    {
        return new RefreshToken
        {
            ApplicationUserId = applicationUserId,
            TokenHash = tokenHash,
            CreatedAtUtc = createdAtUtc,
            ExpiresAtUtc = expiresAtUtc,
            SecurityStamp = securityStamp ?? string.Empty
        };
    }

    public void Revoke(DateTime nowUtc, Guid? replacedByTokenId = null)
    {
        RevokedAtUtc = nowUtc;
        ReplacedByTokenId = replacedByTokenId;
    }
}