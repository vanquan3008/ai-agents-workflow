using DevAgent.Application.Common;
using Microsoft.Extensions.DependencyInjection;

namespace DevAgent.Application.Features.Auth;

internal static class AuthModule
{
    public static IServiceCollection AddAuthHandlers(this IServiceCollection services) => services
        .AddScoped<SessionIssuer>()
        .AddScoped<ICommandHandler<LoginCommand, AuthSession>, LoginHandler>()
        .AddScoped<ICommandHandler<RefreshSessionCommand, AuthSession>, RefreshSessionHandler>()
        .AddScoped<ICommandHandler<LogoutCommand, bool>, LogoutHandler>()
        .AddScoped<ICommandHandler<ChangePasswordCommand, AuthSession>, ChangePasswordHandler>()
        .AddScoped<IQueryHandler<GetCurrentUserQuery, CurrentUserDto>, GetCurrentUserHandler>();
}
