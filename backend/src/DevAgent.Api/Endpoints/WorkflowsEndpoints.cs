using DevAgent.Api.Common;
using DevAgent.Application.Common;
using DevAgent.Application.Features.Workflows;

namespace DevAgent.Api.Endpoints;

internal static class WorkflowsEndpoints
{
    /// <param name="ExpectedVersion">Phiên bản đang xem; khác phiên bản hiện hành thì trả 409.</param>
    public sealed record UpdateWorkflowRequest(int ExpectedVersion, WorkflowDefinitionDto Definition, string? Comment);

    public static RouteGroupBuilder MapWorkflowsEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/repositories/{repositoryId:guid}/workflow").WithTags("Workflows");

        group.MapGet("/", async (Guid repositoryId, IQueryHandler<GetRepositoryWorkflowQuery, RepositoryWorkflowDto> handler, CancellationToken ct) =>
                (await handler.HandleAsync(new GetRepositoryWorkflowQuery(repositoryId), ct)).ToHttp())
            .WithName("GetRepositoryWorkflow");

        group.MapPut("/", async (
                Guid repositoryId,
                UpdateWorkflowRequest body,
                ICommandHandler<UpdateRepositoryWorkflowCommand, RepositoryWorkflowDto> handler,
                CancellationToken ct) =>
                (await handler.HandleAsync(new UpdateRepositoryWorkflowCommand(repositoryId, body.ExpectedVersion, body.Definition, body.Comment), ct)).ToHttp())
            .WithName("UpdateRepositoryWorkflow");

        group.MapGet("/revisions", async (
                Guid repositoryId,
                int? limit,
                IQueryHandler<ListWorkflowRevisionsQuery, IReadOnlyList<WorkflowRevisionDto>> handler,
                CancellationToken ct) =>
                (await handler.HandleAsync(new ListWorkflowRevisionsQuery(repositoryId, limit), ct)).ToHttp())
            .WithName("ListWorkflowRevisions");

        return api;
    }
}
