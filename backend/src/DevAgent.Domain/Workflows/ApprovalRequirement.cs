namespace DevAgent.Domain.Workflows;

/// <summary>When a human approval is required. There is deliberately no "never": sensitive areas always need approval.</summary>
public enum ApprovalRequirement
{
    /// <summary>Only when the repository policy demands it (sensitive area, outside auto-fix scope, unclassifiable).</summary>
    OnSensitive = 1,

    /// <summary>Always, even for changes inside the auto-fix scope.</summary>
    Always = 2,
}
