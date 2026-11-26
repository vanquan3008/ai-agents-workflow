using DevAgent.Application.Abstractions.Security;
using DevAgent.Application.Orchestration;
using DevAgent.Domain.Common;
using Microsoft.Extensions.Options;

namespace DevAgent.Worker;

internal sealed class WorkerIdentity(IOptions<WorkerOptions> options) : IWorkerIdentity
{
    public string WorkerId { get; } = string.IsNullOrWhiteSpace(options.Value.WorkerId)
        ? $"{Environment.MachineName}-{Environment.ProcessId}"
        : options.Value.WorkerId;
}

/// <summary>
/// Worker không có người dùng: mọi hành động mang danh nghĩa agent/hệ thống.
/// Vì vậy agent không thể gọi các use case cần quyền người dùng như duyệt thay đổi.
/// </summary>
internal sealed class NoCurrentUser : ICurrentUser
{
    public UserId? UserId => null;
}
