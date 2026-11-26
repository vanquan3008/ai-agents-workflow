namespace DevAgent.Domain.Policies;

/// <summary>
/// Domain service that applies a repository policy to proposed (plan) or actual (diff) changes.
/// Re-evaluated before every write and before every push.
/// </summary>
public static class ChangePolicyEvaluator
{
    public static PolicyDecision Evaluate(RepositoryPolicy policy, IReadOnlyCollection<ProposedChange> changes)
    {
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(changes);

        if (changes.Count == 0)
        {
            return new PolicyDecision(PolicyOutcome.NeedsReview, SensitiveArea.None, policy.Version, ["Không có thay đổi nào để đánh giá."]);
        }

        var reasons = new List<string>();
        var areas = SensitiveArea.None;
        var needsReview = false;
        var outsideScope = false;

        if (changes.Count > policy.MaxChangedFiles)
        {
            outsideScope = true;
            reasons.Add($"Số file thay đổi ({changes.Count}) vượt giới hạn {policy.MaxChangedFiles}.");
        }

        var autoScope = policy.AutoFixPaths.Select(p => new PathPattern(p)).ToArray();
        var sensitive = policy.SensitivePaths.Select(r => (Rule: r, Pattern: new PathPattern(r.Pattern))).ToArray();

        foreach (var change in changes)
        {
            var normalized = PathPattern.Normalize(change.Path);
            if (normalized is null)
            {
                needsReview = true;
                reasons.Add($"Đường dẫn không hợp lệ hoặc nằm ngoài workspace: \"{change.Path}\".");
                continue;
            }

            if (change.Kind == ChangeKind.Delete)
            {
                areas |= SensitiveArea.Destructive;
                reasons.Add($"Xóa file \"{normalized}\" là thao tác phá hủy.");
            }

            foreach (var (rule, pattern) in sensitive)
            {
                if (pattern.Matches(normalized))
                {
                    areas |= rule.Area;
                    reasons.Add($"\"{normalized}\" thuộc vùng nhạy cảm {rule.Area} (mẫu \"{rule.Pattern}\").");
                }
            }

            if (!autoScope.Any(p => p.Matches(normalized)))
            {
                outsideScope = true;
                reasons.Add($"\"{normalized}\" nằm ngoài phạm vi sửa tự động.");
            }
        }

        if (needsReview)
        {
            return new PolicyDecision(PolicyOutcome.NeedsReview, areas, policy.Version, reasons);
        }

        if (areas != SensitiveArea.None || outsideScope)
        {
            return new PolicyDecision(PolicyOutcome.RequiresApproval, areas, policy.Version, reasons);
        }

        reasons.Add("Mọi thay đổi nằm trong phạm vi tự động và không chạm vùng nhạy cảm.");
        return new PolicyDecision(PolicyOutcome.AutoApproved, areas, policy.Version, reasons);
    }
}
