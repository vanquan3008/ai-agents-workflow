namespace DevAgent.Application.Abstractions.Messaging;

/// <summary>Processes one job type. Must be idempotent because a job can run again after its lease expires.</summary>
public interface IJobHandler
{
    string JobType { get; }

    Task HandleAsync(LeasedJob job, CancellationToken cancellationToken);

    /// <summary>
    /// Called when the job is out of attempts, including when a worker died on the last attempt. Must update
    /// business state so nothing stays "running" without a job driving it. Must be idempotent.
    /// </summary>
    Task OnDeadLetterAsync(DeadLetterJob job, CancellationToken cancellationToken);
}
