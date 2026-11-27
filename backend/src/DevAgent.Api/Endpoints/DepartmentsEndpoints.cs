using DevAgent.Api.Common;
using DevAgent.Application.Common;
using DevAgent.Application.Features.Departments.Commands;
using DevAgent.Application.Features.Departments.Contracts;
using DevAgent.Application.Features.Departments.Queries;
using DevAgent.Application.Features.Tasks;

namespace DevAgent.Api.Endpoints;

/// <summary>
/// Project-scoped IT department offices. Readers use the published routes; managers edit through <c>/draft</c> and
/// change state with POST commands that carry <c>expectedEditVersion</c> (409 when stale).
/// </summary>
internal static class DepartmentsEndpoints
{
    public sealed record CreateDepartmentRequest(string Template, string? Name, string Slug, IReadOnlyList<Guid>? RepositoryIds);

    public sealed record SaveDraftRequest(int ExpectedEditVersion, DepartmentConfigDto Config);

    public sealed record EditVersionRequest(int ExpectedEditVersion);

    public sealed record ListDepartmentTasksRequest(string? Status, string? Search, int? Page, int? PageSize);

    public static RouteGroupBuilder MapDepartmentsEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/projects/{projectId:guid}/departments").WithTags("Departments");

        group.MapGet("/", async (Guid projectId, bool? includeArchived, IQueryHandler<ListDepartmentsQuery, DepartmentDirectoryDto> handler, CancellationToken ct) =>
                (await handler.HandleAsync(new ListDepartmentsQuery(projectId, includeArchived ?? false), ct)).ToHttp())
            .WithName("ListDepartments");

        group.MapPost("/", async (
                Guid projectId,
                CreateDepartmentRequest body,
                ICommandHandler<CreateDepartmentCommand, DepartmentEditResultDto> handler,
                CancellationToken ct) =>
                (await handler.HandleAsync(new CreateDepartmentCommand(projectId, body.Template, body.Name, body.Slug, body.RepositoryIds), ct))
                .ToHttp(d => Results.Created($"/api/v1/projects/{projectId}/departments/{d.Id}", d)))
            .WithName("CreateDepartment");

        group.MapGet("/options", async (Guid projectId, IQueryHandler<GetDepartmentEditorOptionsQuery, DepartmentEditorOptionsDto> handler, CancellationToken ct) =>
                (await handler.HandleAsync(new GetDepartmentEditorOptionsQuery(projectId), ct)).ToHttp())
            .WithName("GetDepartmentEditorOptions");

        var department = group.MapGroup("/{departmentId:guid}");

        department.MapGet("/", async (Guid projectId, Guid departmentId, IQueryHandler<GetPublishedDepartmentQuery, PublishedDepartmentDto> handler, CancellationToken ct) =>
                (await handler.HandleAsync(new GetPublishedDepartmentQuery(projectId, departmentId), ct)).ToHttp())
            .WithName("GetPublishedDepartment");

        department.MapGet("/tasks", async (
                Guid projectId,
                Guid departmentId,
                [AsParameters] ListDepartmentTasksRequest query,
                IQueryHandler<ListDepartmentTasksQuery, PagedResult<TaskSummaryDto>> handler,
                CancellationToken ct) =>
                (await handler.HandleAsync(
                    new ListDepartmentTasksQuery(projectId, departmentId, query.Status, query.Search, query.Page, query.PageSize), ct)).ToHttp())
            .WithName("ListDepartmentTasks");

        department.MapGet("/tasks/{taskId:guid}", async (
                Guid projectId,
                Guid departmentId,
                Guid taskId,
                IQueryHandler<GetDepartmentTaskQuery, TaskDetailDto> handler,
                CancellationToken ct) =>
                (await handler.HandleAsync(new GetDepartmentTaskQuery(projectId, departmentId, taskId), ct)).ToHttp())
            .WithName("GetDepartmentTask");

        department.MapGet("/revisions", async (
                Guid projectId,
                Guid departmentId,
                int? limit,
                IQueryHandler<ListDepartmentRevisionsQuery, IReadOnlyList<DepartmentRevisionDto>> handler,
                CancellationToken ct) =>
                (await handler.HandleAsync(new ListDepartmentRevisionsQuery(projectId, departmentId, limit), ct)).ToHttp())
            .WithName("ListDepartmentRevisions");

        department.MapGet("/draft", async (Guid projectId, Guid departmentId, IQueryHandler<GetDepartmentDraftQuery, DepartmentDraftDto> handler, CancellationToken ct) =>
                (await handler.HandleAsync(new GetDepartmentDraftQuery(projectId, departmentId), ct)).ToHttp())
            .WithName("GetDepartmentDraft");

        department.MapPut("/draft", async (
                Guid projectId,
                Guid departmentId,
                SaveDraftRequest body,
                ICommandHandler<SaveDepartmentDraftCommand, DepartmentDraftDto> handler,
                CancellationToken ct) =>
                (await handler.HandleAsync(new SaveDepartmentDraftCommand(projectId, departmentId, body.ExpectedEditVersion, body.Config), ct)).ToHttp())
            .WithName("SaveDepartmentDraft");

        department.MapPost("/publish", async (
                Guid projectId,
                Guid departmentId,
                EditVersionRequest body,
                ICommandHandler<PublishDepartmentCommand, DepartmentEditResultDto> handler,
                CancellationToken ct) =>
                (await handler.HandleAsync(new PublishDepartmentCommand(projectId, departmentId, body.ExpectedEditVersion), ct)).ToHttp())
            .WithName("PublishDepartment");

        department.MapPost("/revisions/{version:int}/restore", async (
                Guid projectId,
                Guid departmentId,
                int version,
                EditVersionRequest body,
                ICommandHandler<RestoreDepartmentRevisionCommand, DepartmentDraftDto> handler,
                CancellationToken ct) =>
                (await handler.HandleAsync(new RestoreDepartmentRevisionCommand(projectId, departmentId, version, body.ExpectedEditVersion), ct)).ToHttp())
            .WithName("RestoreDepartmentRevision");

        department.MapPost("/archive", async (
                Guid projectId,
                Guid departmentId,
                EditVersionRequest body,
                ICommandHandler<ArchiveDepartmentCommand, DepartmentEditResultDto> handler,
                CancellationToken ct) =>
                (await handler.HandleAsync(new ArchiveDepartmentCommand(projectId, departmentId, body.ExpectedEditVersion), ct)).ToHttp())
            .WithName("ArchiveDepartment");

        return api;
    }
}
