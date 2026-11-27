using DevAgent.Domain.Common;
using DevAgent.Domain.Users;

namespace DevAgent.Domain.Users.Repositories;

public interface IRefreshTokenRepository
{
    Task<RefreshToken?> FindByHashAsync(string tokenHash, CancellationToken cancellationToken);

    Task<IReadOnlyList<RefreshToken>> ListActiveByFamilyAsync(Guid familyId, CancellationToken cancellationToken);

    Task<IReadOnlyList<RefreshToken>> ListActiveByUserAsync(UserId userId, CancellationToken cancellationToken);

    void Add(RefreshToken token);
}
