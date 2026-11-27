using DevAgent.Domain.Common;
using DevAgent.Domain.Users;
using DevAgent.Domain.Users.Repositories;
using Microsoft.EntityFrameworkCore;

namespace DevAgent.Infrastructure.Persistence.Repositories;

internal sealed class RefreshTokenRepository(DevAgentDbContext context) : IRefreshTokenRepository
{
    public Task<RefreshToken?> FindByHashAsync(string tokenHash, CancellationToken cancellationToken) =>
        context.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

    public async Task<IReadOnlyList<RefreshToken>> ListActiveByFamilyAsync(Guid familyId, CancellationToken cancellationToken) =>
        await context.RefreshTokens.Where(t => t.FamilyId == familyId && t.RevokedAt == null).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<RefreshToken>> ListActiveByUserAsync(UserId userId, CancellationToken cancellationToken) =>
        await context.RefreshTokens.Where(t => t.UserId == userId && t.RevokedAt == null).ToListAsync(cancellationToken);

    public void Add(RefreshToken token) => context.RefreshTokens.Add(token);
}
