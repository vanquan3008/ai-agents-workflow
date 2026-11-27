namespace DevAgent.Domain.Users;

/// <summary>A Discord, GitHub or OIDC identity linked to a DevAgent user.</summary>
public sealed class ExternalIdentity
{
    private ExternalIdentity()
    {
        ExternalId = string.Empty;
    }

    internal ExternalIdentity(IdentityProvider provider, string externalId, DateTimeOffset linkedAt)
    {
        Provider = provider;
        ExternalId = externalId;
        LinkedAt = linkedAt;
    }

    public IdentityProvider Provider { get; private set; }

    public string ExternalId { get; private set; }

    public DateTimeOffset LinkedAt { get; private set; }
}
