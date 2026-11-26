namespace DevAgent.Application.Abstractions.Integrations;

/// <summary>Tọa độ repo trên GitHub. Token không bao giờ đi qua Application hay ngữ cảnh model.</summary>
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

/// <summary>
/// Cổng tới hệ quản lý mã nguồn (GitHub App). Hiện thực tự cấp token installation ngắn hạn.
/// </summary>
public interface ISourceControlGateway
{
    Task<string> GetBranchHeadAsync(RepositoryCoordinates repository, string branch, CancellationToken cancellationToken);

    Task<IReadOnlyList<RepositoryFile>> ListFilesAsync(RepositoryCoordinates repository, string commitSha, int maxFiles, CancellationToken cancellationToken);

    /// <summary>Tìm PR theo nhánh task (phục hồi khi Worker mất kết nối sau khi GitHub đã tạo PR).</summary>
    Task<PullRequestInfo?> FindPullRequestAsync(RepositoryCoordinates repository, string headBranch, CancellationToken cancellationToken);

    Task<PullRequestInfo> CreatePullRequestAsync(CreatePullRequestRequest request, CancellationToken cancellationToken);
}

public enum ChatNotificationKind
{
    TaskAccepted = 1,
    ApprovalRequired = 2,
    ClarificationNeeded = 3,
    Completed = 4,
    Failed = 5,
    Blocked = 6,
    Cancelled = 7,
}

/// <param name="DeduplicationKey">Khóa ổn định của thông báo; kênh chat dùng để chống gửi trùng khi job chạy lại.</param>
public sealed record ChatNotification(
    ChatNotificationKind Kind,
    Guid TaskId,
    Guid ProjectId,
    string Title,
    string Message,
    string? Link,
    string? SourceReference,
    string DeduplicationKey);

/// <summary>
/// Gửi thông báo về kênh chat (Discord). Hiện thực ném <see cref="Messaging.RetryableJobException"/> khi lỗi tạm thời
/// (giới hạn tần suất, lỗi mạng, 5xx) và <see cref="Messaging.PermanentJobException"/> khi cấu hình sai (401/403/404).
/// </summary>
public interface IChatNotifier
{
    Task NotifyAsync(ChatNotification notification, CancellationToken cancellationToken);
}

public sealed record SandboxSpec(Guid ExecutionId, string Image, string WorkspaceRef, IReadOnlyDictionary<string, string> Environment);

public sealed record SandboxCommandResult(int ExitCode, string StdOut, string StdErr, TimeSpan Duration, bool TimedOut);

/// <summary>Phiên sandbox cho một lần chạy. Dispose phải thu hồi container trong mọi trường hợp.</summary>
public interface ISandboxSession : IAsyncDisposable
{
    Task<SandboxCommandResult> RunAsync(string command, TimeSpan timeout, CancellationToken cancellationToken);

    Task<string> ReadFileAsync(string relativePath, CancellationToken cancellationToken);

    Task WriteFileAsync(string relativePath, string content, CancellationToken cancellationToken);

    Task<string> GetDiffAsync(CancellationToken cancellationToken);
}

public interface ISandboxManager
{
    Task<ISandboxSession> CreateAsync(SandboxSpec spec, CancellationToken cancellationToken);
}
