using DevAgent.Application.Abstractions.Integrations;
using DevAgent.Application.Common;

namespace DevAgent.Infrastructure.Integrations;

/// <summary>
/// Placeholder until the Docker sandbox exists. Limits are already modelled in <see cref="Options.SandboxOptions"/>:
/// non-root, CPU/RAM/time caps, no network, no host mounts and no Docker socket.
/// </summary>
internal sealed class UnavailableSandboxManager : ISandboxManager
{
    public Task<ISandboxSession> CreateAsync(SandboxSpec spec, CancellationToken cancellationToken) =>
        throw new IntegrationNotConfiguredException("Sandbox chưa được triển khai (Phase 2B); không thể chạy lệnh build/test.");
}
