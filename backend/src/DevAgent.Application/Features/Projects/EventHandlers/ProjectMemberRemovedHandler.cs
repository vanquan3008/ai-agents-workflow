using DevAgent.Application.Abstractions.Events;
using DevAgent.Application.Abstractions.Realtime;
using DevAgent.Domain.Projects;

namespace DevAgent.Application.Features.Projects.EventHandlers;

/// <summary>Removes a former member from the project's realtime groups once the removal has committed.</summary>
internal sealed class ProjectMemberRemovedHandler(IRealtimeAccessRevoker revoker) : IDomainEventHandler<ProjectMemberRemoved>
{
    public Task HandleAsync(ProjectMemberRemoved domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        return revoker.RevokeProjectAsync(domainEvent.UserId, domainEvent.ProjectId, cancellationToken);
    }
}
