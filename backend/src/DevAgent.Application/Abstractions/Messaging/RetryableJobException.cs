namespace DevAgent.Application.Abstractions.Messaging;

/// <summary>Transient failure: the job is retried, after <see cref="RetryAfter"/> when the remote service specified one (e.g. HTTP 429).</summary>
public sealed class RetryableJobException : Exception
{
    public RetryableJobException()
    {
    }

    public RetryableJobException(string message)
        : base(message)
    {
    }

    public RetryableJobException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public TimeSpan? RetryAfter { get; init; }
}
