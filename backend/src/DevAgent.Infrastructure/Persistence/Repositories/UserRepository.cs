using DevAgent.Domain.Common;
using DevAgent.Domain.Users;
using DevAgent.Domain.Users.Repositories;
using Microsoft.EntityFrameworkCore;

namespace DevAgent.Infrastructure.Persistence.Repositories;

internal sealed class UserRepository(DevAgentDbContext context) : IUserRepository
{
    public Task<User?> GetByIdAsync(UserId id, CancellationToken cancellationToken) =>
        context.Users.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

    public Task<User?> FindByExternalIdentityAsync(IdentityProvider provider, string externalId, CancellationToken cancellationToken) =>
        context.Users.FirstOrDefaultAsync(u => u.Identities.Any(i => i.Provider == provider && i.ExternalId == externalId), cancellationToken);

    public Task<User?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken) =>
        context.Users.FirstOrDefaultAsync(u => u.Email == normalizedEmail, cancellationToken);

    public Task<int> CountActiveSuperAdminsAsync(CancellationToken cancellationToken) =>
        context.Users.CountAsync(u => u.SystemRole == SystemRole.SuperAdmin && u.IsActive, cancellationToken);

    public void Add(User user) => context.Users.Add(user);
}
