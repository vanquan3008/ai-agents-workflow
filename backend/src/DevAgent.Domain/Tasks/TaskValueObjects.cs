using System.Text.RegularExpressions;
using DevAgent.Domain.Common;

namespace DevAgent.Domain.Tasks;

/// <summary>Ba kênh nhận việc (checklist 0.1) và API trực tiếp.</summary>
public enum TaskChannel
{
    Dashboard = 1,
    Discord = 2,
    GitHubIssue = 3,
    Api = 4,
}

/// <summary>Nguồn yêu cầu: kênh và tham chiếu bên ngoài (message id Discord, số issue GitHub...).</summary>
public sealed record TaskSource
{
    public TaskSource(TaskChannel channel, string? externalReference)
    {
        Channel = channel;
        ExternalReference = Guard.Optional(externalReference, "external_reference", 500);
    }

    public TaskChannel Channel { get; }

    public string? ExternalReference { get; }
}

/// <summary>
/// Khóa chống gửi trùng. Cùng (project, khóa) chỉ sinh một task.
/// Discord/GitHub dùng id sự kiện; dashboard/API dùng header <c>Idempotency-Key</c>.
/// </summary>
public sealed partial record IdempotencyKey
{
    public const int MaxLength = 200;

    public IdempotencyKey(string value)
    {
        var trimmed = Guard.NotEmpty(value, "idempotency_key", MaxLength);
        if (!AllowedChars().IsMatch(trimmed))
        {
            throw new DomainException("idempotency_key.invalid", "Idempotency key chỉ gồm chữ, số và các ký tự . _ : -");
        }

        Value = trimmed;
    }

    public string Value { get; }

    public override string ToString() => Value;

    [GeneratedRegex("^[A-Za-z0-9._:-]+$", RegexOptions.CultureInvariant)]
    private static partial Regex AllowedChars();
}

/// <summary>Liên kết PR đã mở cho task. Task chỉ được Completed khi đã lưu được giá trị này.</summary>
public sealed record PullRequestLink
{
    public PullRequestLink(int number, string url, string headBranch, string headCommitSha)
    {
        if (number <= 0)
        {
            throw new DomainException("pull_request.number_invalid", "Số PR không hợp lệ.");
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
        {
            throw new DomainException("pull_request.url_invalid", "URL PR phải là https tuyệt đối.");
        }

        Number = number;
        Url = url;
        HeadBranch = Guard.NotEmpty(headBranch, "head_branch", 255);
        HeadCommitSha = CommitSha.Validate(headCommitSha);
    }

    public int Number { get; }

    public string Url { get; }

    public string HeadBranch { get; }

    public string HeadCommitSha { get; }
}

public static partial class CommitSha
{
    public static string Validate(string? sha)
    {
        var value = Guard.NotEmpty(sha, "commit_sha", 64).ToLowerInvariant();
        if (!Pattern().IsMatch(value))
        {
            throw new DomainException("commit_sha.invalid", "Commit SHA không hợp lệ.");
        }

        return value;
    }

    [GeneratedRegex("^[0-9a-f]{7,64}$", RegexOptions.CultureInvariant)]
    private static partial Regex Pattern();
}
