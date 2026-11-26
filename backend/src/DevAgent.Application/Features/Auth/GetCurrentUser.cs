using DevAgent.Application.Authorization;
using DevAgent.Application.Common;

namespace DevAgent.Application.Features.Auth;

public sealed record GetCurrentUserQuery;

internal sealed class GetCurrentUserHandler(ProjectAccess access) : IQueryHandler<GetCurrentUserQuery, CurrentUserDto>
{
    public async Task<Result<CurrentUserDto>> HandleAsync(GetCurrentUserQuery query, CancellationToken cancellationToken)
    {
        var user = await access.CurrentActiveUserAsync(cancellationToken);
        return user is null ? Error.Unauthorized() : CurrentUserDto.From(user);
    }
}
