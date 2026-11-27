using DevAgent.Application.Abstractions.Security;
using DevAgent.Application.AgentRuntime.Jobs;
using DevAgent.Domain.Common;
using Microsoft.Extensions.Options;

namespace DevAgent.Worker.Hosting;

internal sealed class WorkerIdentity(IOptions<WorkerOptions> options) : IWorkerIdentity
{
    public string WorkerId { get; } = string.IsNullOrWhiteSpace(options.Value.WorkerId)
        ? $"{Environment.MachineName}-{Environment.ProcessId}"
        : options.Value.WorkerId;
}

/// <summary>
/// The worker has no user: every action runs as an agent or the system, so agents can never invoke
/// use cases that need a user's permission, such as approving changes.
/// </summary>
internal sealed class NoCurrentUser : ICurrentUser
{
    public UserId? UserId => null;
}
