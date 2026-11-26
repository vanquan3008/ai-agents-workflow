namespace DevAgent.Application.Abstractions.Integrations;

/// <summary>Repository coordinates on GitHub. Access tokens never pass through the application layer or model context.</summary>
public sealed record RepositoryCoordinates(long InstallationId, string Owner, string Name);

public sealed record RepositoryFile(string Path, long SizeBytes);

public sealed record PullRequestInfo(int Number, string Url, string HeadBranch, string HeadCommitSha, string State);

public sealed record CreatePullRequestRequest(
    RepositoryCoordinates Repository,
    string BaseBranch,
    string HeadBranch,
    string ExpectedHeadCommitSha,
    string Title,
    string Body);

/// <summary>Port to the source control host (GitHub App). Implementations mint short-lived installation tokens themselves.</summary>
public interface ISourceControlGateway
{
    Task<string> GetBranchHeadAsync(RepositoryCoordinates repository, string branch, CancellationToken cancellationToken);

    Task<IReadOnlyList<RepositoryFile>> ListFilesAsync(RepositoryCoordinates repository, string commitSha, int maxFiles, CancellationToken cancellationToken);

    /// <summary>Finds a PR by task branch, used to recover when a worker crashed after GitHub created the PR.</summary>
    Task<PullRequestInfo?> FindPullRequestAsync(RepositoryCoordinates repository, string headBranch, CancellationToken cancellationToken);

    Task<PullRequestInfo> CreatePullRequestAsync(CreatePullRequestRequest request, CancellationToken cancellationToken);
}
