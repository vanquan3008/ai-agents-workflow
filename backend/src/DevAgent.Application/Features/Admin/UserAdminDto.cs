namespace DevAgent.Application.Features.Admin;

public sealed record UserAdminDto(
    Guid Id,
    string Email,
    string DisplayName,
    string SystemRole,
    bool IsActive,
    bool MustChangePassword,
    DateTimeOffset? LockedUntil,
    DateTimeOffset? LastLoginAt,
    int ProjectCount,
    DateTimeOffset CreatedAt);
