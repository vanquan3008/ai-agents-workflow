using DevAgent.Application.Common;
using Microsoft.Extensions.DependencyInjection;

namespace DevAgent.Application.Features.Admin;

internal static class UserAdminModule
{
    public static IServiceCollection AddUserAdminModule(this IServiceCollection services) => services
        .AddScoped<IQueryHandler<ListUsersQuery, PagedResult<UserAdminDto>>, ListUsersHandler>()
        .AddScoped<ICommandHandler<CreateUserCommand, UserAdminDto>, CreateUserHandler>()
        .AddScoped<ICommandHandler<UpdateUserCommand, UserAdminDto>, UpdateUserHandler>()
        .AddScoped<ICommandHandler<ResetUserPasswordCommand, bool>, ResetUserPasswordHandler>();
}
