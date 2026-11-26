using System.Security.Cryptography;
using System.Text;
using DevAgent.Application.Abstractions.Integrations;
using DevAgent.Application.Common;

namespace DevAgent.Infrastructure.Integrations;

/// <summary>
/// GitHub is unavailable (Mode = Disabled, or Mode = App before the GitHub App adapter exists).
/// Fails loudly so the orchestrator blocks the task with a reason instead of pretending to succeed.
/// </summary>
internal sealed class UnavailableSourceControlGateway(string reason) : ISourceControlGateway
{
    public Task<string> GetBranchHeadAsync(RepositoryCoordinates repository, string branch, CancellationToken cancellationToken) =>
        throw new IntegrationNotConfiguredException(reason);

    public Task<IReadOnlyList<RepositoryFile>> ListFilesAsync(RepositoryCoordinates repository, string commitSha, int maxFiles, CancellationToken cancellationToken) =>
        throw new IntegrationNotConfiguredException(reason);

    public Task<PullRequestInfo?> FindPullRequestAsync(RepositoryCoordinates repository, string headBranch, CancellationToken cancellationToken) =>
        throw new IntegrationNotConfiguredException(reason);

    public Task<PullRequestInfo> CreatePullRequestAsync(CreatePullRequestRequest request, CancellationToken cancellationToken) =>
        throw new IntegrationNotConfiguredException(reason);
}

/// <summary>
/// Deterministic GitHub stand-in for Development/Testing: SHAs derive from repository and branch, the file list is fixed
/// and PRs live in memory (enough to exercise the "find PR by branch" recovery path).
/// </summary>
internal sealed class FakeSourceControlGateway : ISourceControlGateway
{
    private static readonly string[] Files =
    [
        "README.md",
        "src/Tasks/TaskFilter.cs",
        "src/Tasks/TaskService.cs",
        "src/Auth/PermissionService.cs",
        "tests/Tasks/TaskFilterTests.cs",
        ".github/workflows/ci.yml",
    ];

    private readonly Dictionary<string, PullRequestInfo> _pullRequests = new(StringComparer.Ordinal);
    private readonly Lock _gate = new();

    public Task<string> GetBranchHeadAsync(RepositoryCoordinates repository, string branch, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(repository);
        return Task.FromResult(Sha($"{repository.Owner}/{repository.Name}@{branch}"));
    }

    public Task<IReadOnlyList<RepositoryFile>> ListFilesAsync(RepositoryCoordinates repository, string commitSha, int maxFiles, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<RepositoryFile>>(Files.Take(maxFiles).Select(f => new RepositoryFile(f, 1024)).ToArray());

    public Task<PullRequestInfo?> FindPullRequestAsync(RepositoryCoordinates repository, string headBranch, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(repository);
        lock (_gate)
        {
            return Task.FromResult(_pullRequests.GetValueOrDefault(Key(repository, headBranch)));
        }
    }

    public Task<PullRequestInfo> CreatePullRequestAsync(CreatePullRequestRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        lock (_gate)
        {
            var key = Key(request.Repository, request.HeadBranch);
            if (!_pullRequests.TryGetValue(key, out var pr))
            {
                var number = _pullRequests.Count + 1;
                pr = new PullRequestInfo(
                    number,
                    $"https://github.com/{request.Repository.Owner}/{request.Repository.Name}/pull/{number}",
                    request.HeadBranch,
                    request.ExpectedHeadCommitSha,
                    "open");
                _pullRequests[key] = pr;
            }

            return Task.FromResult(pr);
        }
    }

    private static string Key(RepositoryCoordinates repository, string branch) => $"{repository.Owner}/{repository.Name}#{branch}";

    private static string Sha(string seed) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(seed)))[..40];
}
