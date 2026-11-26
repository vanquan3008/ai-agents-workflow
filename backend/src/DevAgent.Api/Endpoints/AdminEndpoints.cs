using DevAgent.Api.Auth;
using DevAgent.Api.Common;
using DevAgent.Application.Common;
using DevAgent.Application.Features.Admin;

namespace DevAgent.Api.Endpoints;

/// <summary>Quản trị người dùng (Super Admin). Use case kiểm tra lại quyền từ DB, policy ở đây là lớp chặn sớm.</summary>
internal static class AdminEndpoints
{
    public sealed record CreateUserRequest(string Email, string DisplayName, string SystemRole, string TemporaryPassword);

    public sealed record UpdateUserRequest(string? DisplayName, string? SystemRole, bool? IsActive);

    public sealed record ResetPasswordRequest(string TemporaryPassword);

    public sealed record ListUsersRequest(string? Search, int? Page, int? PageSize);

    public static RouteGroupBuilder MapAdminEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/admin/users").WithTags("Admin").RequireAuthorization(AuthExtensions.SuperAdminPolicy);

        group.MapGet("/", async ([AsParameters] ListUsersRequest query, IQueryHandler<ListUsersQuery, PagedResult<UserAdminDto>> handler, CancellationToken ct) =>
                (await handler.HandleAsync(new ListUsersQuery(query.Search, query.Page, query.PageSize), ct)).ToHttp())
            .WithName("ListUsers");

        group.MapPost("/", async (CreateUserRequest body, ICommandHandler<CreateUserCommand, UserAdminDto> handler, CancellationToken ct) =>
                (await handler.HandleAsync(new CreateUserCommand(body.Email, body.DisplayName, body.SystemRole, body.TemporaryPassword), ct))
                .ToHttp(u => Results.Created($"/api/v1/admin/users/{u.Id}", u)))
            .WithName("CreateUser");

        group.MapPatch("/{userId:guid}", async (Guid userId, UpdateUserRequest body, ICommandHandler<UpdateUserCommand, UserAdminDto> handler, CancellationToken ct) =>
                (await handler.HandleAsync(new UpdateUserCommand(userId, body.DisplayName, body.SystemRole, body.IsActive), ct)).ToHttp())
            .WithName("UpdateUser");

        group.MapPost("/{userId:guid}/reset-password", async (Guid userId, ResetPasswordRequest body, ICommandHandler<ResetUserPasswordCommand, bool> handler, CancellationToken ct) =>
                (await handler.HandleAsync(new ResetUserPasswordCommand(userId, body.TemporaryPassword), ct)).ToHttp(_ => Results.NoContent()))
            .WithName("ResetUserPassword");

        return api;
    }
}
