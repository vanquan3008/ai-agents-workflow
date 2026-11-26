namespace DevAgent.Application.Common;

/// <summary>An external integration (GitHub, Discord, AI, sandbox) is not configured or is disabled.</summary>
public sealed class IntegrationNotConfiguredException : Exception
{
    public IntegrationNotConfiguredException()
    {
    }

    public IntegrationNotConfiguredException(string message)
        : base(message)
    {
    }

    public IntegrationNotConfiguredException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
