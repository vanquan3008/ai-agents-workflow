namespace DevAgent.Domain.Policies;

public enum PolicyOutcome
{
    /// <summary>Entirely inside the auto-fix scope and touches no sensitive area.</summary>
    AutoApproved = 1,

    /// <summary>Touches a sensitive area, leaves the auto-fix scope or exceeds limits: an approver must decide.</summary>
    RequiresApproval = 2,

    /// <summary>Could not be classified (invalid paths, no changes): stop for a human to review.</summary>
    NeedsReview = 3,
}
