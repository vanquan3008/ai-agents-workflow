namespace DevAgent.Domain.Policies;

public enum PolicyOutcome
{
    /// <summary>Nằm trọn trong phạm vi tự động, không chạm vùng nhạy cảm.</summary>
    AutoApproved = 1,

    /// <summary>Chạm vùng nhạy cảm, nằm ngoài phạm vi tự động hoặc vượt giới hạn: cần người có quyền duyệt.</summary>
    RequiresApproval = 2,

    /// <summary>Không phân loại được (đường dẫn không hợp lệ, không có thay đổi): dừng để người phụ trách xem xét.</summary>
    NeedsReview = 3,
}

public enum ChangeKind
{
    Create = 1,
    Modify = 2,
    Delete = 3,
    Rename = 4,
}

public sealed record ProposedChange(string Path, ChangeKind Kind);

public sealed record PolicyDecision(PolicyOutcome Outcome, SensitiveArea Areas, int PolicyVersion, IReadOnlyList<string> Reasons)
{
    public bool CanProceedAutomatically => Outcome == PolicyOutcome.AutoApproved;
}

/// <summary>
/// Domain service áp chính sách repo lên tập thay đổi dự kiến (kế hoạch) hoặc thực tế (diff).
/// Được gọi lại trước mỗi thao tác ghi và trước khi push (checklist 2A, 2C).
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
