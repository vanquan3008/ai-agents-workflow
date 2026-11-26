using DevAgent.Domain.Common;
using DevAgent.Domain.Repositories;
using DevAgent.Domain.Tasks;
using DevAgent.Domain.Workflows;

namespace DevAgent.Application.Orchestration.Steps;

/// <summary>Ngữ cảnh một lần tiến task: aggregate đã tải, repo, quy trình hiệu lực (ảnh chụp của task) và Worker.</summary>
public sealed record WorkflowContext(AgentTask Task, CodeRepository Repository, WorkflowDefinition Workflow, string WorkerId);

/// <summary>
/// Một bước của quy trình. Orchestrator chọn bước theo trạng thái task; mỗi lần chạy chỉ tiến một bước,
/// lưu kết quả và xếp job cho bước sau (điểm lưu tiến độ). Bước phải idempotent theo trạng thái.
/// </summary>
public interface IWorkflowStep
{
    /// <summary>Tên bước, dùng trong log/trace.</summary>
    string Name { get; }

    bool CanHandle(AgentTaskStatus status);

    Task ExecuteAsync(WorkflowContext context, CancellationToken cancellationToken);
}

internal static class StepActors
{
    public static readonly Actor Orchestrator = Actor.ForAgent("orchestrator");
    public static readonly Actor Planner = Actor.ForAgent("planner");
}
