using DevAgent.Domain.Common;

namespace DevAgent.Domain.Tasks;

/// <summary>The pull request opened for a task. A task can only complete once this link is stored.</summary>
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
