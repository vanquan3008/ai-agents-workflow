namespace DevAgent.Application.Common;

/// <summary>Unique constraint violation (e.g. a reused idempotency key), translated from the database error.</summary>
public sealed class DuplicateKeyException : Exception
{
    public DuplicateKeyException()
    {
    }

    public DuplicateKeyException(string message)
        : base(message)
    {
    }

    public DuplicateKeyException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public string? ConstraintName { get; init; }
}
