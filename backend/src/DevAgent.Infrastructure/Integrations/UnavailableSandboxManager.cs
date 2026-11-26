using DevAgent.Application.Abstractions.Integrations;
using DevAgent.Application.Common;

namespace DevAgent.Infrastructure.Integrations;

/// <summary>
/// Sandbox chưa triển khai (Phase 2B). Cấu hình giới hạn đã có trong <see cref="Options.SandboxOptions"/>;
/// adapter Docker sẽ áp dụng: non-root, giới hạn CPU/RAM/thời gian, chặn mạng, không mount host/Docker socket.
/// </summary>
internal sealed class UnavailableSandboxManager : ISandboxManager
{
    public Task<ISandboxSession> CreateAsync(SandboxSpec spec, CancellationToken cancellationToken) =>
        throw new IntegrationNotConfiguredException("Sandbox chưa được triển khai (Phase 2B); không thể chạy lệnh build/test.");
}
