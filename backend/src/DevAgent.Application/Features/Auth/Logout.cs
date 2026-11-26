using DevAgent.Application.Abstractions.Persistence;
using DevAgent.Application.Abstractions.Security;
using DevAgent.Application.Common;
using DevAgent.Domain.Users;
using DevAgent.Domain.Users.Repositories;

namespace DevAgent.Application.Features.Auth;

public sealed record LogoutCommand(string? RefreshToken);

internal sealed class LogoutHandler(IRefreshTokenRepository refreshTokens, ITokenService tokens, IUnitOfWork unitOfWork, TimeProvider clock)
    : ICommandHandler<LogoutCommand, bool>
{
    public async Task<Result<bool>> HandleAsync(LogoutCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (string.IsNullOrWhiteSpace(command.RefreshToken))
        {
            return true;
        }

        var token = await refreshTokens.FindByHashAsync(tokens.HashRefreshToken(command.RefreshToken), cancellationToken);
        if (token is not null)
        {
            var now = clock.GetUtcNow();
            foreach (var sibling in await refreshTokens.ListActiveByFamilyAsync(token.FamilyId, cancellationToken))
            {
                sibling.Revoke("logout", now);
            }

            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return true;
    }
}
