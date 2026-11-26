using DevAgent.Domain.Common;
using DevAgent.Domain.Users;

namespace DevAgent.Domain.Users.Repositories;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(UserId id, CancellationToken cancellationToken);

    Task<User?> FindByExternalIdentityAsync(IdentityProvider provider, string externalId, CancellationToken cancellationToken);

    /// <summary>Looks up by the normalized (lower-case) email.</summary>
    Task<User?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken);

    Task<int> CountActiveSuperAdminsAsync(CancellationToken cancellationToken);

    void Add(User user);
}
