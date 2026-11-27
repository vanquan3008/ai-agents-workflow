using DevAgent.Domain.Common;

namespace DevAgent.Domain.Users;

/// <summary>
/// Rotating refresh token. Only the hash is stored; the raw value lives in the browser cookie.
/// Tokens issued in sequence share a <see cref="FamilyId"/>: reusing an already rotated token signals theft
/// and revokes the whole family.
/// </summary>
public sealed class RefreshToken : AggregateRoot<RefreshTokenId>
{
    private RefreshToken()
    {
        TokenHash = string.Empty;
    }

    private RefreshToken(RefreshTokenId id, UserId userId, string tokenHash, Guid familyId, DateTimeOffset createdAt, DateTimeOffset expiresAt, string? createdByIp, string? userAgent)
        : base(id)
    {
        UserId = userId;
        TokenHash = tokenHash;
        FamilyId = familyId;
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
        CreatedByIp = createdByIp;
        UserAgent = userAgent;
    }

    public UserId UserId { get; private set; }

    public string TokenHash { get; private set; }

    public Guid FamilyId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    public string? RevokedReason { get; private set; }

    public RefreshTokenId? ReplacedBy { get; private set; }

    public string? CreatedByIp { get; private set; }

    public string? UserAgent { get; private set; }

    public bool IsActive(DateTimeOffset now) => RevokedAt is null && ExpiresAt > now;

    public bool WasRotated => ReplacedBy is not null;

    public static RefreshToken Issue(UserId userId, string tokenHash, Guid? familyId, DateTimeOffset now, TimeSpan lifetime, string? ip, string? userAgent)
    {
        if (lifetime <= TimeSpan.Zero)
        {
            throw new DomainException("refresh_token.lifetime_invalid", "Thời hạn refresh token phải dương.");
        }

        return new RefreshToken(
            RefreshTokenId.New(),
            Guard.NotDefault(userId, "user_id"),
            Guard.NotEmpty(tokenHash, "token_hash", 200),
            familyId ?? Guid.CreateVersion7(),
            Guard.Utc(now, "created_at"),
            now + lifetime,
            Guard.Optional(ip, "ip", 64),
            Guard.Optional(userAgent, "user_agent", 500));
    }

    /// <summary>Revokes this token and points it at its successor in the same family.</summary>
    public void RotateTo(RefreshTokenId next, DateTimeOffset now)
    {
        if (!IsActive(now))
        {
            throw new DomainException("refresh_token.inactive", "Refresh token không còn hiệu lực.");
        }

        ReplacedBy = next;
        RevokedAt = now;
        RevokedReason = "rotated";
    }

    public void Revoke(string reason, DateTimeOffset now)
    {
        if (RevokedAt is null)
        {
            RevokedAt = Guard.Utc(now, "revoked_at");
            RevokedReason = Guard.NotEmpty(reason, "reason", 100);
        }
    }
}
