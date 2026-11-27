using DevAgent.Application.Abstractions.Events;
using DevAgent.Application.Common;
using DevAgent.Application.Features.Tasks.EventHandlers;
using DevAgent.Domain.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace DevAgent.Application.Features.Tasks;

internal static class TasksModule
{
    public static IServiceCollection AddTasksModule(this IServiceCollection services) => services
        .AddScoped<ICommandHandler<CreateTaskCommand, TaskCreatedDto>, CreateTaskHandler>()
        .AddScoped<ICommandHandler<CancelTaskCommand, string>, CancelTaskHandler>()
        .AddScoped<ICommandHandler<ProvideClarificationCommand, string>, ProvideClarificationHandler>()
        .AddScoped<ICommandHandler<RetryTaskCommand, string>, RetryTaskHandler>()
        .AddScoped<IQueryHandler<ListTasksQuery, PagedResult<TaskSummaryDto>>, ListTasksHandler>()
        .AddScoped<IQueryHandler<GetTaskQuery, TaskDetailDto>, GetTaskHandler>()
        .AddScoped<IQueryHandler<GetTaskTimelineQuery, IReadOnlyList<TimelineEntryDto>>, GetTaskTimelineHandler>()
        .AddScoped<IQueryHandler<AuthorizeTaskSubscriptionQuery, Guid>, AuthorizeTaskSubscriptionHandler>()
        .AddScoped<IDomainEventHandler<TaskStatusChanged>, TaskStatusChangedHandler>()
        .AddScoped<IDomainEventHandler<TaskPlanCreated>, TaskPlanCreatedHandler>();
}
