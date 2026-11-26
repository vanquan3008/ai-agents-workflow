using DevAgent.Domain.Common;

namespace DevAgent.Domain.Workflows;

public sealed record PullRequestSettings
{
    public const int MaxLabels = 10;
    public const int MaxReviewers = 10;

    public PullRequestSettings(bool draft, IReadOnlyList<string> labels, string titlePrefix, IReadOnlyList<string> reviewers)
    {
        ArgumentNullException.ThrowIfNull(labels);
        ArgumentNullException.ThrowIfNull(reviewers);

        if (labels.Count > MaxLabels)
        {
            throw new DomainException("workflow.pr_labels_too_many", $"Tối đa {MaxLabels} nhãn PR.");
        }

        if (reviewers.Count > MaxReviewers)
        {
            throw new DomainException("workflow.pr_reviewers_too_many", $"Tối đa {MaxReviewers} người review.");
        }

        Draft = draft;
        Labels = labels.Select(l => Guard.NotEmpty(l, "pr_label", 50)).Distinct(StringComparer.Ordinal).ToArray();
        TitlePrefix = Guard.Optional(titlePrefix, "pr_title_prefix", 50) ?? string.Empty;
        Reviewers = reviewers.Select(ValidateLogin).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public bool Draft { get; }

    public IReadOnlyList<string> Labels { get; }

    public string TitlePrefix { get; }

    /// <summary>GitHub logins requested as reviewers. Merging remains a human decision on GitHub.</summary>
    public IReadOnlyList<string> Reviewers { get; }

    private static string ValidateLogin(string login)
    {
        var value = Guard.NotEmpty(login, "pr_reviewer", 39);
        return WorkflowPatterns.GitHubLogin().IsMatch(value)
            ? value
            : throw new DomainException("workflow.pr_reviewer_invalid", $"GitHub login không hợp lệ: {value}.");
    }
}
