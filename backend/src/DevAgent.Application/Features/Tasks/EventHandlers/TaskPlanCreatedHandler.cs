using DevAgent.Application.Abstractions.Events;
using DevAgent.Application.Abstractions.Realtime;
using DevAgent.Domain.Tasks;

namespace DevAgent.Application.Features.Tasks.EventHandlers;

internal sealed class TaskPlanCreatedHandler(ITaskEventPublisher publisher) : IDomainEventHandler<TaskPlanCreated>
{
    public Task HandleAsync(TaskPlanCreated domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        return publisher.PublishAsync(
            new TaskRealtimeEvent(
                domainEvent.TaskId.Value,
                domainEvent.ProjectId.Value,
                "task.plan_created",
                0,
                null,
                $"Kế hoạch v{domainEvent.PlanVersion}",
                domainEvent.OccurredAt),
            cancellationToken);
    }
}
