using DevAgent.Domain.Common;

namespace DevAgent.Domain.Users;

public enum IdentityProvider
{
    Oidc = 1,
    GitHub = 2,
    Discord = 3,
}

/// <summary>Danh tính bên ngoài (Discord/GitHub/OIDC) được liên kết với một người dùng DevAgent.</summary>
public sealed class ExternalIdentity
{
    private ExternalIdentity()
    {
        ExternalId = string.Empty;
    }

    internal ExternalIdentity(IdentityProvider provider, string externalId, DateTimeOffset linkedAt)
    {
        Provider = provider;
        ExternalId = externalId;
        LinkedAt = linkedAt;
    }

    public IdentityProvider Provider { get; private set; }

    public string ExternalId { get; private set; }

    public DateTimeOffset LinkedAt { get; private set; }
}

/// <summary>
/// Người dùng DevAgent. Quyền hiệu lực luôn tính từ <see cref="Projects.ProjectMember"/>,
/// không bao giờ từ nội dung tin nhắn, issue hay đầu ra của model.
/// </summary>
public sealed class User : AggregateRoot<UserId>
{
    private readonly List<ExternalIdentity> _identities = [];

    private User()
    {
        DisplayName = string.Empty;
    }

    private User(UserId id, string displayName, string? email, DateTimeOffset createdAt)
        : base(id)
    {
        DisplayName = displayName;
        Email = email;
        CreatedAt = createdAt;
    }

    public string DisplayName { get; private set; }

    public string? Email { get; private set; }

    public bool IsActive { get; private set; } = true;

    public DateTimeOffset CreatedAt { get; private set; }

    public IReadOnlyCollection<ExternalIdentity> Identities => _identities.AsReadOnly();

    public static User Create(string displayName, string? email, DateTimeOffset now) =>
        Create(UserId.New(), displayName, email, now);

    /// <summary>Tạo với Id xác định trước (seed dữ liệu môi trường phát triển, đồng bộ từ IdP).</summary>
    public static User Create(UserId id, string displayName, string? email, DateTimeOffset now) =>
        new(
            Guard.NotDefault(id, "user_id"),
            Guard.NotEmpty(displayName, "display_name", 200),
            Guard.Optional(email, "email", 320),
            Guard.Utc(now, "created_at"));

    public void LinkIdentity(IdentityProvider provider, string externalId, DateTimeOffset now)
    {
        var id = Guard.NotEmpty(externalId, "external_id", 200);
        if (_identities.Any(i => i.Provider == provider))
        {
            throw new DomainException("user.identity_already_linked", $"Người dùng đã liên kết {provider}.");
        }

        _identities.Add(new ExternalIdentity(provider, id, Guard.Utc(now, "linked_at")));
    }

    public void Deactivate() => IsActive = false;
}
