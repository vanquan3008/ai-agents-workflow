using DevAgent.Application.Abstractions.Persistence;
using DevAgent.Application.Common;
using DevAgent.Domain.Tasks;

namespace DevAgent.Application.Orchestration.Steps;

/// <summary>
/// Coding / Testing / Reviewer / Publisher thuộc backlog Phase 2B–2C. Bước này dừng task rõ ràng trước mọi thao tác ghi,
/// thay vì giả lập kết quả. Khi hiện thực bước thật, đăng ký <see cref="IWorkflowStep"/> mới cho trạng thái tương ứng
/// và bỏ trạng thái đó khỏi đây.
/// </summary>
internal sealed class PendingImplementationStep(IUnitOfWork unitOfWork, TimeProvider clock) : IWorkflowStep
{
    private static readonly AgentTaskStatus[] Handled =
        [AgentTaskStatus.InProgress, AgentTaskStatus.Testing, AgentTaskStatus.Reviewing, AgentTaskStatus.Publishing];

    public string Name => "pending-implementation";

    public bool CanHandle(AgentTaskStatus status) => Handled.Contains(status);

    public async Task ExecuteAsync(WorkflowContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.Task.Block(
            $"Bước {ApiNames.ToApi(context.Task.Status)} chưa được triển khai (Phase 2B/2C). Task dừng trước mọi thao tác ghi.",
            StepActors.Orchestrator,
            clock.GetUtcNow());
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
