namespace DevAgent.Application.Abstractions.Messaging;

/// <summary>
/// A job to enqueue. <paramref name="DeduplicationKey"/> is unique: enqueueing the same key twice yields one job.
/// </summary>
public sealed record JobRequest(string Type, string PayloadJson, string DeduplicationKey, DateTimeOffset? AvailableAt = null);

/// <summary>
/// Producer side of the durable queue. Jobs are written in the same transaction as the business change
/// (transactional outbox), so a job exists if and only if the change was saved.
/// </summary>
public interface IJobQueue
{
    Task EnqueueAsync(JobRequest request, CancellationToken cancellationToken);
}
