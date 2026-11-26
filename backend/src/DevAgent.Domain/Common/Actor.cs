namespace DevAgent.Domain.Common;

public enum ActorKind
{
    User = 1,
    Agent = 2,
    System = 3,
}

/// <summary>Ai đã thực hiện hành động: người dùng, agent hay hệ thống. Dùng cho lịch sử trạng thái và audit.</summary>
public sealed record Actor
{
    private Actor(ActorKind kind, string id)
    {
        Kind = kind;
        Id = id;
    }

    public ActorKind Kind { get; }

    public string Id { get; }

    public static Actor System { get; } = new(ActorKind.System, "system");

    public static Actor ForUser(UserId userId) => new(ActorKind.User, userId.ToString());

    public static Actor ForAgent(string agentName) => new(ActorKind.Agent, Guard.NotEmpty(agentName, "agent", 100));

    public static Actor Restore(ActorKind kind, string id) => new(kind, id);

    public bool IsUser => Kind == ActorKind.User;

    public override string ToString() => $"{Kind}:{Id}";
}
