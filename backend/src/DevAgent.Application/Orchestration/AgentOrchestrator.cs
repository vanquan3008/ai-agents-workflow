using DevAgent.Application.Abstractions.Persistence;
using DevAgent.Application.Common;
using DevAgent.Application.Options;
using DevAgent.Application.Orchestration.Steps;
using DevAgent.Domain.Common;
using DevAgent.Domain.Executions;
using DevAgent.Domain.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DevAgent.Application.Orchestration;

/// <summary>
/// Điều phối tuần tự Planner → Coding → Testing → Reviewer → Publisher (checklist 0.5).
/// Mỗi lần chạy chỉ tiến task thêm một bước (<see cref="IWorkflowStep"/>) rồi xếp job cho bước sau,
/// nên mỗi bước là một điểm lưu tiến độ: Worker chết giữa chừng thì Worker khác nhận lại job sau khi lease hết hạn.
/// </summary>
public interface IAgentOrchestrator
{
    Task AdvanceAsync(TaskId taskId, string workerId, CancellationToken cancellationToken);

    /// <summary>Đánh dấu task thất bại (vd job đã hết lượt thử). Idempotent: task đã kết thúc thì bỏ qua.</summary>
    Task MarkFailedAsync(TaskId taskId, string reason, CancellationToken cancellationToken);
}

internal sealed partial class AgentOrchestrator(
    ITaskRepository tasks,
    ICodeRepositoryRepository repositories,
    IExecutionRepository executions,
    IUnitOfWork unitOfWork,
    IEnumerable<IWorkflowStep> steps,
    IOptions<AgentLimitsOptions> limits,
    TimeProvider clock,
    ILogger<AgentOrchestrator> logger) : IAgentOrchestrator
{
    public async Task AdvanceAsync(TaskId taskId, string workerId, CancellationToken cancellationToken)
    {
        var task = await tasks.GetByIdAsync(taskId, cancellationToken);
        if (task is null)
        {
            LogTaskMissing(logger, taskId);
            return;
        }

        if (!AgentTaskStateMachine.IsActive(task.Status))
        {
            // Đã hủy, đang chờ duyệt, đã xong...: không làm gì, chỉ dọn execution còn treo.
            await CloseDanglingExecutionsAsync(task, reason: null, cancellationToken);
            return;
        }

        var now = clock.GetUtcNow();
        if (task.ActiveDuration(now) > TimeSpan.FromMinutes(limits.Value.TaskTimeoutMinutes))
        {
            task.TimeOut($"Vượt thời gian xử lý tối đa {limits.Value.TaskTimeoutMinutes} phút.", now);
            await CloseDanglingExecutionsAsync(task, reason: null, cancellationToken, save: false);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return;
        }

        var repository = await repositories.GetByIdAsync(task.RepositoryId, cancellationToken);
        if (repository is null || !repository.IsActive)
        {
            task.Block("Repo không còn được đăng ký hoặc đã bị vô hiệu hóa.", StepActors.Orchestrator, now);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return;
        }

        var step = steps.FirstOrDefault(s => s.CanHandle(task.Status))
            ?? throw new InvalidOperationException($"Không có bước nào xử lý trạng thái {task.Status}.");

        // Task đang chạy dùng ảnh chụp quy trình của nó; task chưa bắt đầu dùng quy trình hiện hành của repo.
        var workflow = task.Workflow ?? repository.Workflow;
        using var activity = DevAgentTelemetry.ActivitySource.StartActivity($"step {step.Name}");
        activity?.SetTag(DevAgentTelemetry.TaskIdTag, task.Id.ToString());

        try
        {
            await step.ExecuteAsync(new WorkflowContext(task, repository, workflow, workerId), cancellationToken);
        }
        catch (IntegrationNotConfiguredException ex)
        {
            LogIntegrationMissing(logger, taskId, ex.Message);
            task.Block(ex.Message, StepActors.Orchestrator, clock.GetUtcNow());
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task MarkFailedAsync(TaskId taskId, string reason, CancellationToken cancellationToken)
    {
        var task = await tasks.GetByIdAsync(taskId, cancellationToken);
        if (task is null)
        {
            return;
        }

        if (AgentTaskStateMachine.CanTransition(task.Status, AgentTaskStatus.Failed))
        {
            task.Fail(reason, StepActors.Orchestrator, clock.GetUtcNow());
        }

        // Dù task đã ở trạng thái kết thúc, execution còn "đang chạy" vẫn phải được đóng với lý do.
        await CloseDanglingExecutionsAsync(task, reason, cancellationToken, save: false);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Đóng execution còn treo: thất bại kèm lý do nếu có, hết giờ nếu task hết giờ, còn lại là hủy.</summary>
    private async Task CloseDanglingExecutionsAsync(AgentTask task, string? reason, CancellationToken cancellationToken, bool save = true)
    {
        var running = await executions.ListRunningByTaskAsync(task.Id, cancellationToken);
        if (running.Count == 0)
        {
            return;
        }

        var now = clock.GetUtcNow();
        foreach (var execution in running)
        {
            if (reason is not null)
            {
                execution.Fail(reason, now);
            }
            else if (task.Status == AgentTaskStatus.TimedOut)
            {
                execution.TimeOut(now);
            }
            else
            {
                execution.Cancel(now);
            }
        }

        if (save)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Không tìm thấy task {TaskId} khi xử lý job")]
    private static partial void LogTaskMissing(ILogger logger, TaskId taskId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Task {TaskId} bị chặn vì tích hợp chưa cấu hình: {Reason}")]
    private static partial void LogIntegrationMissing(ILogger logger, TaskId taskId, string reason);
}
