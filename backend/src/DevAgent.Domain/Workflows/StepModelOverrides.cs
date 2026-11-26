using DevAgent.Domain.Common;

namespace DevAgent.Domain.Workflows;

/// <summary>Per-step model overrides; null falls back to the system default model.</summary>
public sealed record StepModelOverrides
{
    public StepModelOverrides(string? planner, string? coding, string? reviewer)
    {
        Planner = Validate(planner, "planner");
        Coding = Validate(coding, "coding");
        Reviewer = Validate(reviewer, "reviewer");
    }

    public static StepModelOverrides None { get; } = new(null, null, null);

    public string? Planner { get; }

    public string? Coding { get; }

    public string? Reviewer { get; }

    private static string? Validate(string? model, string step)
    {
        var value = Guard.Optional(model, $"model_{step}", 100);
        return value is null || WorkflowPatterns.ModelId().IsMatch(value)
            ? value
            : throw new DomainException("workflow.model_invalid", $"Model cho bước {step} không hợp lệ: {value}.");
    }
}
