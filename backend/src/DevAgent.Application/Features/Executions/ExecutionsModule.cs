using DevAgent.Application.Common;
using Microsoft.Extensions.DependencyInjection;

namespace DevAgent.Application.Features.Executions;

internal static class ExecutionsModule
{
    public static IServiceCollection AddExecutionsModule(this IServiceCollection services) => services
        .AddScoped<IQueryHandler<ListTaskExecutionsQuery, IReadOnlyList<ExecutionSummaryDto>>, ListTaskExecutionsHandler>()
        .AddScoped<IQueryHandler<GetExecutionQuery, ExecutionDetailDto>, GetExecutionHandler>();
}
