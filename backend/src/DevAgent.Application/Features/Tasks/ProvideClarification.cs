using DevAgent.Application.Abstractions.Messaging;
using DevAgent.Application.Abstractions.Persistence;
using DevAgent.Application.Authorization;
using DevAgent.Application.Common;
using DevAgent.Domain.Common;
using DevAgent.Domain.Projects;
using DevAgent.Domain.Tasks;
using DevAgent.Domain.Tasks.Repositories;

namespace DevAgent.Application.Features.Tasks;

public sealed record ProvideClarificationCommand(Guid TaskId, string AdditionalInfo);

internal sealed class ProvideClarificationHandler(
    ProjectAccess access,
    ITaskRepository tasks,
    IJobQueue jobQueue,
    IUnitOfWork unitOfWork,
    TimeProvider clock) : ICommandHandler<ProvideClarificationCommand, string>
{
    public async Task<Result<string>> HandleAsync(ProvideClarificationCommand command, CancellationToken cancellationToken)
    {
        var task = await tasks.GetByIdAsync(new TaskId(command.TaskId), cancellationToken);
        if (task is null)
        {
            return Error.NotFound("task", command.TaskId);
        }

        var userId = await access.RequireAsync(task.ProjectId, ProjectPermission.CreateTasks, "task", task.Id.ToString(), cancellationToken);
        if (userId.IsFailure)
        {
            return userId.Error!;
        }

        if (task.Status != AgentTaskStatus.NeedsClarification)
        {
            return Error.Conflict("task.not_waiting_clarification", "Task không ở trạng thái cần bổ sung thông tin.");
        }

        task.ProvideClarification(command.AdditionalInfo, userId.Value, clock.GetUtcNow());
        await jobQueue.EnqueueAsync(TaskJobs.ProcessTask(task), cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return task.Status.Api();
    }
}
