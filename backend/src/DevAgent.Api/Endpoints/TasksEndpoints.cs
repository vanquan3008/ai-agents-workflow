using DevAgent.Api.Common;
using DevAgent.Application.Common;
using DevAgent.Application.Features.Executions;
using DevAgent.Application.Features.Tasks;
using DevAgent.Domain.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace DevAgent.Api.Endpoints;

internal static class TasksEndpoints
{
    public const string IdempotencyHeader = "Idempotency-Key";

    public sealed record CreateTaskRequest(Guid RepositoryId, string Title, string Description);

    public sealed record CancelTaskRequest(string? Reason);

    public sealed record ClarifyTaskRequest(string AdditionalInfo);

    public static RouteGroupBuilder MapTasksEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/tasks").WithTags("Tasks");

        // Idempotency-Key is required: repeating the same key returns the same task (200 instead of 201).
        group.MapPost("/", async (
                CreateTaskRequest body,
                [FromHeader(Name = IdempotencyHeader)] string? idempotencyKey,
                ICommandHandler<CreateTaskCommand, TaskCreatedDto> handler,
                CancellationToken ct) =>
            {
                var result = await handler.HandleAsync(
                    new CreateTaskCommand(body.RepositoryId, body.Title, body.Description, TaskChannel.Dashboard, null, idempotencyKey ?? string.Empty),
                    ct);
                return result.ToHttp(created => created.Created
                    ? Results.Created($"/api/v1/tasks/{created.TaskId}", created)
                    : Results.Ok(created));
            })
            .RequireRateLimiting(RateLimitPolicies.TaskCreation)
            .WithName("CreateTask");

        group.MapGet("/", async (
                [AsParameters] ListTasksRequest query,
                IQueryHandler<ListTasksQuery, PagedResult<TaskSummaryDto>> handler,
                CancellationToken ct) =>
                (await handler.HandleAsync(
                    new ListTasksQuery(query.ProjectId, query.RepositoryId, query.Status, query.Search, query.Page, query.PageSize), ct)).ToHttp())
            .WithName("ListTasks");

        group.MapGet("/{taskId:guid}", async (Guid taskId, IQueryHandler<GetTaskQuery, TaskDetailDto> handler, CancellationToken ct) =>
                (await handler.HandleAsync(new GetTaskQuery(taskId), ct)).ToHttp())
            .WithName("GetTask");

        // Timeline replay after a SignalR reconnect: ?after=<last sequence the client has>.
        group.MapGet("/{taskId:guid}/timeline", async (
                Guid taskId,
                long? after,
                int? limit,
                IQueryHandler<GetTaskTimelineQuery, IReadOnlyList<TimelineEntryDto>> handler,
                CancellationToken ct) =>
                (await handler.HandleAsync(new GetTaskTimelineQuery(taskId, after ?? 0, limit), ct)).ToHttp())
            .WithName("GetTaskTimeline");

        group.MapGet("/{taskId:guid}/executions", async (Guid taskId, IQueryHandler<ListTaskExecutionsQuery, IReadOnlyList<ExecutionSummaryDto>> handler, CancellationToken ct) =>
                (await handler.HandleAsync(new ListTaskExecutionsQuery(taskId), ct)).ToHttp())
            .WithName("ListTaskExecutions");

        group.MapPost("/{taskId:guid}/cancel", async (Guid taskId, CancelTaskRequest? body, ICommandHandler<CancelTaskCommand, string> handler, CancellationToken ct) =>
                (await handler.HandleAsync(new CancelTaskCommand(taskId, body?.Reason), ct)).ToHttp(status => Results.Ok(new { status })))
            .WithName("CancelTask");

        group.MapPost("/{taskId:guid}/clarifications", async (Guid taskId, ClarifyTaskRequest body, ICommandHandler<ProvideClarificationCommand, string> handler, CancellationToken ct) =>
                (await handler.HandleAsync(new ProvideClarificationCommand(taskId, body.AdditionalInfo), ct)).ToHttp(status => Results.Ok(new { status })))
            .WithName("ClarifyTask");

        group.MapPost("/{taskId:guid}/retry", async (Guid taskId, ICommandHandler<RetryTaskCommand, string> handler, CancellationToken ct) =>
                (await handler.HandleAsync(new RetryTaskCommand(taskId), ct)).ToHttp(status => Results.Ok(new { status })))
            .WithName("RetryTask");

        return api;
    }

    public sealed record ListTasksRequest(Guid? ProjectId, Guid? RepositoryId, string? Status, string? Search, int? Page, int? PageSize);
}
