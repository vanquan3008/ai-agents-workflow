using DevAgent.Application.Authorization;
using DevAgent.Application.Common;

namespace DevAgent.Application.Features.Admin;

public sealed record ListUsersQuery(string? Search, int? Page, int? PageSize);

internal sealed class ListUsersHandler(ProjectAccess access, IUserQueries queries) : IQueryHandler<ListUsersQuery, PagedResult<UserAdminDto>>
{
    public async Task<Result<PagedResult<UserAdminDto>>> HandleAsync(ListUsersQuery query, CancellationToken cancellationToken)
    {
        var admin = await access.RequireSuperAdminAsync("admin.users.list", cancellationToken);
        return admin.IsFailure
            ? Result<PagedResult<UserAdminDto>>.Failure(admin.Error!)
            : await queries.ListAsync(query.Search?.Trim(), new PageRequest(query.Page, query.PageSize), cancellationToken);
    }
}
