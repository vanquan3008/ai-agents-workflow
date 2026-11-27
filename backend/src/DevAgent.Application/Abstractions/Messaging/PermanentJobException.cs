namespace DevAgent.Application.Abstractions.Messaging;

/// <summary>Failure that retrying cannot fix (misconfiguration, missing permission): dead-letter immediately for an operator.</summary>
public sealed class PermanentJobException : Exception
{
    public PermanentJobException()
    {
    }

    public PermanentJobException(string message)
        : base(message)
    {
    }

    public PermanentJobException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
