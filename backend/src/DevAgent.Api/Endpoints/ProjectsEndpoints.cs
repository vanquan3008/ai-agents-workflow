using DevAgent.Api.Common;
using DevAgent.Application.Common;
using DevAgent.Application.Features.Projects;

namespace DevAgent.Api.Endpoints;

internal static class ProjectsEndpoints
{
    public sealed record CreateProjectRequest(string Name, string Slug, string? OwnerEmail);

    public sealed record UpsertMemberRequest(string Email, string Role);

    public sealed record RegisterRepositoryRequest(
        long InstallationId,
        string Owner,
        string Name,
        string DefaultBranch,
        string BuildCommand,
        string TestCommand,
        IReadOnlyList<string>? AutoFixPaths,
        IReadOnlyList<SensitivePathInput>? SensitivePaths,
        int? MaxChangedFiles);

    public static RouteGroupBuilder MapProjectsEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/projects").WithTags("Projects");

        group.MapGet("/", async (IQueryHandler<ListMyProjectsQuery, IReadOnlyList<ProjectDto>> handler, CancellationToken ct) =>
                (await handler.HandleAsync(new ListMyProjectsQuery(), ct)).ToHttp())
            .WithName("ListMyProjects");

        group.MapPost("/", async (CreateProjectRequest body, ICommandHandler<CreateProjectCommand, ProjectDto> handler, CancellationToken ct) =>
                (await handler.HandleAsync(new CreateProjectCommand(body.Name, body.Slug, body.OwnerEmail), ct))
                .ToHttp(p => Results.Created($"/api/v1/projects/{p.Id}", p)))
            .WithName("CreateProject");

        group.MapGet("/{projectId:guid}", async (Guid projectId, IQueryHandler<GetProjectQuery, ProjectDto> handler, CancellationToken ct) =>
                (await handler.HandleAsync(new GetProjectQuery(projectId), ct)).ToHttp())
            .WithName("GetProject");

        group.MapGet("/{projectId:guid}/members", async (Guid projectId, IQueryHandler<ListProjectMembersQuery, IReadOnlyList<ProjectMemberDto>> handler, CancellationToken ct) =>
                (await handler.HandleAsync(new ListProjectMembersQuery(projectId), ct)).ToHttp())
            .WithName("ListProjectMembers");

        group.MapPut("/{projectId:guid}/members", async (Guid projectId, UpsertMemberRequest body, ICommandHandler<UpsertProjectMemberCommand, bool> handler, CancellationToken ct) =>
                (await handler.HandleAsync(new UpsertProjectMemberCommand(projectId, body.Email, body.Role), ct)).ToHttp(_ => Results.NoContent()))
            .WithName("UpsertProjectMember");

        group.MapDelete("/{projectId:guid}/members/{userId:guid}", async (Guid projectId, Guid userId, ICommandHandler<RemoveProjectMemberCommand, bool> handler, CancellationToken ct) =>
                (await handler.HandleAsync(new RemoveProjectMemberCommand(projectId, userId), ct)).ToHttp(_ => Results.NoContent()))
            .WithName("RemoveProjectMember");

        group.MapGet("/{projectId:guid}/repositories", async (Guid projectId, IQueryHandler<ListRepositoriesQuery, IReadOnlyList<RepositoryDto>> handler, CancellationToken ct) =>
                (await handler.HandleAsync(new ListRepositoriesQuery(projectId), ct)).ToHttp())
            .WithName("ListRepositories");

        group.MapPost("/{projectId:guid}/repositories", async (Guid projectId, RegisterRepositoryRequest body, ICommandHandler<RegisterRepositoryCommand, RepositoryDto> handler, CancellationToken ct) =>
                (await handler.HandleAsync(
                    new RegisterRepositoryCommand(
                        projectId, body.InstallationId, body.Owner, body.Name, body.DefaultBranch, body.BuildCommand, body.TestCommand,
                        body.AutoFixPaths, body.SensitivePaths, body.MaxChangedFiles),
                    ct))
                .ToHttp(r => Results.Created($"/api/v1/projects/{projectId}/repositories/{r.Id}", r)))
            .WithName("RegisterRepository");

        return api;
    }
}
