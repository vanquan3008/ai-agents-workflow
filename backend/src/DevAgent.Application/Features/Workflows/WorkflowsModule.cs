using DevAgent.Application.Common;
using Microsoft.Extensions.DependencyInjection;

namespace DevAgent.Application.Features.Workflows;

internal static class WorkflowsModule
{
    public static IServiceCollection AddWorkflowsModule(this IServiceCollection services) => services
        .AddScoped<IQueryHandler<GetRepositoryWorkflowQuery, RepositoryWorkflowDto>, GetRepositoryWorkflowHandler>()
        .AddScoped<IQueryHandler<ListWorkflowRevisionsQuery, IReadOnlyList<WorkflowRevisionDto>>, ListWorkflowRevisionsHandler>()
        .AddScoped<ICommandHandler<UpdateRepositoryWorkflowCommand, RepositoryWorkflowDto>, UpdateRepositoryWorkflowHandler>();
}
