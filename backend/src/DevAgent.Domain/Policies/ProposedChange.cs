namespace DevAgent.Domain.Policies;

public sealed record ProposedChange(string Path, ChangeKind Kind);
