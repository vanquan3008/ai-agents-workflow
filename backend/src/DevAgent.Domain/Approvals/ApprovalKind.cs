namespace DevAgent.Domain.Approvals;

public enum ApprovalKind
{
    /// <summary>Approve the plan before sensitive changes are made.</summary>
    Plan = 1,

    /// <summary>Approve the actual diff, bound to a commit, before the PR is published.</summary>
    Diff = 2,
}
