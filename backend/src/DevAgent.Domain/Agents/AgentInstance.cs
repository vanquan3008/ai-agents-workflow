using DevAgent.Domain.Common;

namespace DevAgent.Domain.Agents;

/// <summary>
/// A registered worker process that sends periodic heartbeats. Used to observe operations and detect dead workers;
/// the right to process a job is decided by the queue lease, not by this record.
/// </summary>
public sealed class AgentInstance : AggregateRoot<AgentInstanceId>
{
    private AgentInstance()
    {
        WorkerId = string.Empty;
        HostName = string.Empty;
    }

    private AgentInstance(AgentInstanceId id, string workerId, string hostName, DateTimeOffset startedAt)
        : base(id)
    {
        WorkerId = workerId;
        HostName = hostName;
        StartedAt = startedAt;
        LastHeartbeatAt = startedAt;
        Status = AgentInstanceStatus.Idle;
    }

    public string WorkerId { get; private set; }

    public string HostName { get; private set; }

    public AgentInstanceStatus Status { get; private set; }

    public int ActiveJobs { get; private set; }

    public DateTimeOffset StartedAt { get; private set; }

    public DateTimeOffset LastHeartbeatAt { get; private set; }

    public static AgentInstance Register(string workerId, string hostName, DateTimeOffset now) =>
        new(AgentInstanceId.New(), Guard.NotEmpty(workerId, "worker_id", 200), Guard.NotEmpty(hostName, "host_name", 200), Guard.Utc(now, "started_at"));

    public void Heartbeat(int activeJobs, DateTimeOffset now)
    {
        if (Status == AgentInstanceStatus.Stopped)
        {
            throw new DomainException("agent_instance.stopped", "Instance đã dừng.");
        }

        ActiveJobs = Math.Max(0, activeJobs);
        Status = ActiveJobs > 0 ? AgentInstanceStatus.Busy : AgentInstanceStatus.Idle;
        LastHeartbeatAt = Guard.Utc(now, "heartbeat_at");
    }

    /// <summary>Reuses the record when a worker restarts with the same id (WorkerId is unique).</summary>
    public void Restart(string hostName, DateTimeOffset now)
    {
        HostName = Guard.NotEmpty(hostName, "host_name", 200);
        StartedAt = Guard.Utc(now, "started_at");
        LastHeartbeatAt = now;
        ActiveJobs = 0;
        Status = AgentInstanceStatus.Idle;
    }

    public void Stop(DateTimeOffset now)
    {
        Status = AgentInstanceStatus.Stopped;
        ActiveJobs = 0;
        LastHeartbeatAt = Guard.Utc(now, "heartbeat_at");
    }

    public bool IsStale(DateTimeOffset now, TimeSpan threshold) => Status != AgentInstanceStatus.Stopped && now - LastHeartbeatAt > threshold;
}
