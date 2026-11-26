using System.Text.Json;
using DevAgent.Application.Abstractions.Messaging;
using DevAgent.Application.Common;
using DevAgent.Domain.Tasks;

namespace DevAgent.Application.Features.Tasks;

/// <summary>Payload of a <see cref="JobTypes.ProcessTask"/> job.</summary>
public sealed record ProcessTaskPayload(Guid TaskId);

internal static class TaskJobs
{
    /// <summary>
    /// The deduplication key includes the latest transition sequence, so each status produces exactly one
    /// processing job no matter how often the use case or worker repeats.
    /// </summary>
    public static JobRequest ProcessTask(AgentTask task) => new(
        JobTypes.ProcessTask,
        JsonSerializer.Serialize(new ProcessTaskPayload(task.Id.Value)),
        $"{JobTypes.ProcessTask}:{task.Id}:{task.Transitions.Max(t => t.Sequence)}");
}

internal static class TaskMapping
{
    public static string Api(this AgentTaskStatus status) => ApiNames.ToApi(status);
}
