using DevAgent.Application.Common;
using DevAgent.Domain.Common;

namespace DevAgent.Application.Features.Admin;

public interface IUserQueries
{
    Task<PagedResult<UserAdminDto>> ListAsync(string? search, PageRequest page, CancellationToken cancellationToken);

    Task<UserAdminDto?> GetAsync(UserId userId, CancellationToken cancellationToken);
}
