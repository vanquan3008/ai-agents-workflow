namespace DevAgent.Application.Abstractions.Integrations;

public sealed record SandboxSpec(Guid ExecutionId, string Image, string WorkspaceRef, IReadOnlyDictionary<string, string> Environment);

public sealed record SandboxCommandResult(int ExitCode, string StdOut, string StdErr, TimeSpan Duration, bool TimedOut);

/// <summary>Sandbox session for one execution. Disposing must reclaim the container in every case.</summary>
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
