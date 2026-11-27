using DevAgent.Domain.Common;

namespace DevAgent.Domain.Tasks;

/// <summary>Where a request came from: the channel plus an external reference such as a Discord message id or issue number.</summary>
public sealed record TaskSource
{
    public TaskSource(TaskChannel channel, string? externalReference)
    {
        Channel = channel;
        ExternalReference = Guard.Optional(externalReference, "external_reference", 500);
    }

    public TaskChannel Channel { get; }

    public string? ExternalReference { get; }
}
