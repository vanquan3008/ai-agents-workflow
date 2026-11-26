namespace DevAgent.Application.AgentRuntime.Jobs;

/// <summary>Identity of the current worker process, recorded on leases, executions and agent instances.</summary>
public interface IWorkerIdentity
{
    string WorkerId { get; }
}
