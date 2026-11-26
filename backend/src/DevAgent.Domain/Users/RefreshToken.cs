using DevAgent.Domain.Common;

namespace DevAgent.Domain.Users;

public readonly record struct RefreshTokenId(Guid Value)
{
    public static RefreshTokenId New() => new(Guid.CreateVersion7());

    public override string ToString() => Value.ToString();
}

/// <summary>
/// Refresh token xoay vòng. Chỉ lưu băm của token (token thật chỉ nằm trong cookie của trình duyệt).
/// Các token sinh nối tiếp nhau chung một <see cref="FamilyId"/>: nếu một token đã bị thay thế mà vẫn được dùng
/// lại (dấu hiệu bị đánh cắp), cả họ token bị thu hồi.
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

    /// <summary>Token đã được dùng để xoay sang token mới.</summary>
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

    /// <summary>Xoay vòng: thu hồi token này và trỏ tới token kế tiếp cùng họ.</summary>
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
