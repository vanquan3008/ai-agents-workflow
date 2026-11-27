namespace DevAgent.Domain.Users;

public readonly record struct RefreshTokenId(Guid Value)
{
    public static RefreshTokenId New() => new(Guid.CreateVersion7());

    public override string ToString() => Value.ToString();
}
