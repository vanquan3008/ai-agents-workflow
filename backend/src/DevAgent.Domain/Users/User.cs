using System.Text.RegularExpressions;
using DevAgent.Domain.Common;

namespace DevAgent.Domain.Users;

public enum IdentityProvider
{
    Oidc = 1,
    GitHub = 2,
    Discord = 3,
}

/// <summary>Vai trò cấp hệ thống. Quyền theo project vẫn tính từ <see cref="Projects.ProjectMember"/>.</summary>
public enum SystemRole
{
    User = 1,

    /// <summary>Quản trị nền tảng: toàn quyền trên mọi project, tạo project, quản lý người dùng.</summary>
    SuperAdmin = 2,
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
/// Người dùng DevAgent đăng nhập bằng email + mật khẩu. Domain không biết thuật toán băm: chỉ giữ chuỗi băm
/// do tầng Application tạo. <see cref="SecurityStamp"/> đổi mỗi khi thông tin bảo mật đổi (mật khẩu, vai trò,
/// khóa tài khoản), làm mọi access token cũ mất hiệu lực.
/// </summary>
public sealed partial class User : AggregateRoot<UserId>
{
    private readonly List<ExternalIdentity> _identities = [];

    private User()
    {
        DisplayName = string.Empty;
        Email = string.Empty;
        SecurityStamp = string.Empty;
    }

    private User(UserId id, string email, string displayName, SystemRole role, DateTimeOffset createdAt)
        : base(id)
    {
        Email = email;
        DisplayName = displayName;
        SystemRole = role;
        CreatedAt = createdAt;
        SecurityStamp = NewStamp();
    }

    public string Email { get; private set; }

    public string DisplayName { get; private set; }

    public SystemRole SystemRole { get; private set; }

    public bool IsSuperAdmin => SystemRole == SystemRole.SuperAdmin;

    /// <summary>Chuỗi băm mật khẩu (định dạng do <c>IPasswordHasher</c> quyết định). Null = chưa đặt mật khẩu.</summary>
    public string? PasswordHash { get; private set; }

    /// <summary>Bắt đổi mật khẩu ở lần đăng nhập tới (mật khẩu tạm do quản trị đặt hoặc seed).</summary>
    public bool MustChangePassword { get; private set; }

    public string SecurityStamp { get; private set; }

    public int FailedLoginCount { get; private set; }

    public DateTimeOffset? LockoutEndsAt { get; private set; }

    public DateTimeOffset? LastLoginAt { get; private set; }

    public bool IsActive { get; private set; } = true;

    public DateTimeOffset CreatedAt { get; private set; }

    public IReadOnlyCollection<ExternalIdentity> Identities => _identities.AsReadOnly();

    public static User Create(string email, string displayName, SystemRole role, DateTimeOffset now) =>
        Create(UserId.New(), email, displayName, role, now);

    public static User Create(UserId id, string email, string displayName, SystemRole role, DateTimeOffset now) =>
        new(
            Guard.NotDefault(id, "user_id"),
            NormalizeEmail(email),
            Guard.NotEmpty(displayName, "display_name", 200),
            Enum.IsDefined(role) ? role : throw new DomainException("user.role_invalid", "Vai trò hệ thống không hợp lệ."),
            Guard.Utc(now, "created_at"));

    /// <summary>Chuẩn hóa email để so khớp đăng nhập không phân biệt hoa thường.</summary>
    public static string NormalizeEmail(string email)
    {
        var value = Guard.NotEmpty(email, "email", 320).ToLowerInvariant();
        return EmailPattern().IsMatch(value) ? value : throw new DomainException("user.email_invalid", "Email không hợp lệ.");
    }

    public bool IsLockedOut(DateTimeOffset now) => LockoutEndsAt is { } until && until > now;

    public void SetPassword(string passwordHash, bool mustChange)
    {
        PasswordHash = Guard.NotEmpty(passwordHash, "password_hash", 500);
        MustChangePassword = mustChange;
        FailedLoginCount = 0;
        LockoutEndsAt = null;
        SecurityStamp = NewStamp();
    }

    /// <summary>Đổi chuỗi băm sang tham số mạnh hơn mà không đổi mật khẩu (không làm mất phiên đăng nhập).</summary>
    public void UpgradePasswordHash(string passwordHash) => PasswordHash = Guard.NotEmpty(passwordHash, "password_hash", 500);

    /// <summary>Ghi nhận đăng nhập sai; đủ số lần thì khóa tạm thời.</summary>
    public void RecordFailedLogin(DateTimeOffset now, int maxAttempts, TimeSpan lockoutDuration)
    {
        FailedLoginCount++;
        if (FailedLoginCount >= maxAttempts)
        {
            LockoutEndsAt = Guard.Utc(now, "now") + lockoutDuration;
            FailedLoginCount = 0;
        }
    }

    public void RecordSuccessfulLogin(DateTimeOffset now)
    {
        FailedLoginCount = 0;
        LockoutEndsAt = null;
        LastLoginAt = Guard.Utc(now, "last_login_at");
    }

    public void ChangeRole(SystemRole role)
    {
        if (!Enum.IsDefined(role))
        {
            throw new DomainException("user.role_invalid", "Vai trò hệ thống không hợp lệ.");
        }

        if (role != SystemRole)
        {
            SystemRole = role;
            SecurityStamp = NewStamp();
        }
    }

    public void Rename(string displayName) => DisplayName = Guard.NotEmpty(displayName, "display_name", 200);

    public void Deactivate()
    {
        IsActive = false;
        SecurityStamp = NewStamp();
    }

    public void Activate()
    {
        IsActive = true;
        FailedLoginCount = 0;
        LockoutEndsAt = null;
        SecurityStamp = NewStamp();
    }

    public void LinkIdentity(IdentityProvider provider, string externalId, DateTimeOffset now)
    {
        var id = Guard.NotEmpty(externalId, "external_id", 200);
        if (_identities.Any(i => i.Provider == provider))
        {
            throw new DomainException("user.identity_already_linked", $"Người dùng đã liên kết {provider}.");
        }

        _identities.Add(new ExternalIdentity(provider, id, Guard.Utc(now, "linked_at")));
    }

    private static string NewStamp() => Guid.NewGuid().ToString("N");

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.CultureInvariant)]
    private static partial Regex EmailPattern();
}
