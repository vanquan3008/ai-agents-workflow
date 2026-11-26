using DevAgent.Api.Common;
using DevAgent.Application.Common;
using DevAgent.Application.Features.Approvals;
using DevAgent.Application.Features.Executions;

namespace DevAgent.Api.Endpoints;

internal static class ApprovalsEndpoints
{
    public sealed record DecisionRequest(string? Comment);

    public sealed record ListApprovalsRequest(Guid? ProjectId, Guid? TaskId, string? Status, int? Page, int? PageSize);

    public static RouteGroupBuilder MapApprovalsEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/approvals").WithTags("Approvals");

        group.MapGet("/", async ([AsParameters] ListApprovalsRequest query, IQueryHandler<ListApprovalsQuery, PagedResult<ApprovalDto>> handler, CancellationToken ct) =>
                (await handler.HandleAsync(new ListApprovalsQuery(query.ProjectId, query.TaskId, query.Status, query.Page, query.PageSize), ct)).ToHttp())
            .WithName("ListApprovals");

        group.MapGet("/{approvalId:guid}", async (Guid approvalId, IQueryHandler<GetApprovalQuery, ApprovalDto> handler, CancellationToken ct) =>
                (await handler.HandleAsync(new GetApprovalQuery(approvalId), ct)).ToHttp())
            .WithName("GetApproval");

        group.MapPost("/{approvalId:guid}/approve", async (Guid approvalId, DecisionRequest? body, ICommandHandler<DecideApprovalCommand, ApprovalDto> handler, CancellationToken ct) =>
                (await handler.HandleAsync(new DecideApprovalCommand(approvalId, ApprovalDecision.Approve, body?.Comment), ct)).ToHttp())
            .WithName("ApproveApproval");

        group.MapPost("/{approvalId:guid}/reject", async (Guid approvalId, DecisionRequest body, ICommandHandler<DecideApprovalCommand, ApprovalDto> handler, CancellationToken ct) =>
                (await handler.HandleAsync(new DecideApprovalCommand(approvalId, ApprovalDecision.Reject, body.Comment), ct)).ToHttp())
            .WithName("RejectApproval");

        return api;
    }

    public static RouteGroupBuilder MapExecutionsEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/executions/{executionId:guid}", async (Guid executionId, IQueryHandler<GetExecutionQuery, ExecutionDetailDto> handler, CancellationToken ct) =>
                (await handler.HandleAsync(new GetExecutionQuery(executionId), ct)).ToHttp())
            .WithTags("Executions")
            .WithName("GetExecution");

        return api;
    }
}
