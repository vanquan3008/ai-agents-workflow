using DevAgent.Application.Abstractions.Persistence;
using DevAgent.Application.Authorization;
using DevAgent.Application.Common;
using DevAgent.Application.Options;
using DevAgent.Domain.Auditing;
using DevAgent.Domain.Common;
using DevAgent.Domain.Projects;
using DevAgent.Domain.Workflows;
using Microsoft.Extensions.Options;

namespace DevAgent.Application.Features.Workflows;

public sealed record PullRequestSettingsDto(bool Draft, IReadOnlyList<string> Labels, string TitlePrefix, IReadOnlyList<string> Reviewers);

public sealed record ModelOverridesDto(string? Planner, string? Coding, string? Reviewer);

public sealed record WorkflowDefinitionDto(
    string PlanApproval,
    string DiffApproval,
    int PlannerMaxAttempts,
    int MaxFixIterations,
    string ReviewerBlockingSeverity,
    bool RequireRegressionTest,
    PullRequestSettingsDto PullRequest,
    IReadOnlyList<string> NotifyOn,
    ModelOverridesDto Models);

/// <summary>Giới hạn hệ thống áp lên quy trình, để giao diện hiển thị và kiểm tra trước khi gửi.</summary>
public sealed record WorkflowLimitsDto(
    int PlannerMaxAttemptsCap,
    int MaxFixIterationsCap,
    IReadOnlyList<string> ApprovalOptions,
    IReadOnlyList<string> SeverityOptions,
    IReadOnlyList<string> NotificationOptions,
    IReadOnlyList<string> MandatoryNotifications,
    IReadOnlyList<string> Invariants);

public sealed record RepositoryWorkflowDto(Guid RepositoryId, string RepositoryFullName, int Version, bool IsDefault, WorkflowDefinitionDto Definition, WorkflowLimitsDto Limits);

public sealed record WorkflowRevisionDto(int Version, Guid ChangedBy, DateTimeOffset ChangedAt, string? Comment, WorkflowDefinitionDto Definition);

internal static class WorkflowMapping
{
    /// <summary>Ràng buộc không cấu hình được qua quy trình (hệ thống luôn áp dụng).</summary>
    public static readonly IReadOnlyList<string> Invariants =
    [
        "Các bước Planner → Coding → Testing → Reviewer → Publisher luôn chạy theo thứ tự.",
        "Thay đổi chạm vùng nhạy cảm (xác thực, phân quyền, migration, credential, CI/CD, xóa file) luôn cần người duyệt.",
        "Agent không thể tự duyệt; quyết định duyệt gắn với người duyệt, phiên bản kế hoạch hoặc commit.",
        "Build/test lỗi hoặc còn phát hiện chặn thì không xuất bản PR.",
        "Con người merge PR trên GitHub; không triển khai production.",
        "Lệnh chỉ chạy trong sandbox; ngân sách, timeout và số task đồng thời theo cấu hình hệ thống.",
    ];

    public static WorkflowDefinitionDto ToDto(WorkflowDefinition d) => new(
        ApiNames.ToApi(d.PlanApproval),
        ApiNames.ToApi(d.DiffApproval),
        d.PlannerMaxAttempts,
        d.MaxFixIterations,
        ApiNames.ToApi(d.ReviewerBlockingSeverity),
        d.RequireRegressionTest,
        new PullRequestSettingsDto(d.PullRequest.Draft, d.PullRequest.Labels, d.PullRequest.TitlePrefix, d.PullRequest.Reviewers),
        d.NotifyOn.Select(e => ApiNames.ToApi(e)).ToArray(),
        new ModelOverridesDto(d.Models.Planner, d.Models.Coding, d.Models.Reviewer));

    public static WorkflowLimitsDto Limits(AgentLimitsOptions limits) => new(
        WorkflowDefinition.MaxPlannerAttempts,
        Math.Min(WorkflowDefinition.MaxFixIterationsLimit, limits.MaxFixIterations),
        Enum.GetValues<ApprovalRequirement>().Select(v => ApiNames.ToApi(v)).ToArray(),
        Enum.GetValues<FindingSeverity>().Where(v => v <= FindingSeverity.High).Select(v => ApiNames.ToApi(v)).ToArray(),
        Enum.GetValues<NotificationEvent>().Select(v => ApiNames.ToApi(v)).ToArray(),
        WorkflowDefinition.MandatoryNotifications.Select(v => ApiNames.ToApi(v)).ToArray(),
        Invariants);

    /// <summary>Chuyển DTO thành value object; gom lỗi theo trường để trả 400 có chi tiết.</summary>
    public static Result<WorkflowDefinition> FromDto(WorkflowDefinitionDto dto, int fixIterationsCap)
    {
        var errors = new Dictionary<string, string[]>();

        if (!ApiNames.TryParse<ApprovalRequirement>(dto.PlanApproval, out var planApproval))
        {
            errors["planApproval"] = ["Giá trị hợp lệ: ON_SENSITIVE, ALWAYS."];
        }

        if (!ApiNames.TryParse<ApprovalRequirement>(dto.DiffApproval, out var diffApproval))
        {
            errors["diffApproval"] = ["Giá trị hợp lệ: ON_SENSITIVE, ALWAYS."];
        }

        if (!ApiNames.TryParse<FindingSeverity>(dto.ReviewerBlockingSeverity, out var severity) || severity > FindingSeverity.High)
        {
            errors["reviewerBlockingSeverity"] = ["Giá trị hợp lệ: LOW, MEDIUM, HIGH (không được nới tới CRITICAL)."];
        }

        if (dto.MaxFixIterations > fixIterationsCap)
        {
            errors["maxFixIterations"] = [$"Không vượt trần hệ thống {fixIterationsCap} vòng."];
        }

        var notify = new List<NotificationEvent>();
        foreach (var value in dto.NotifyOn ?? [])
        {
            if (ApiNames.TryParse<NotificationEvent>(value, out var e))
            {
                notify.Add(e);
            }
            else
            {
                errors["notifyOn"] = [$"Sự kiện không hợp lệ: {value}."];
            }
        }

        if (errors.Count > 0)
        {
            return Error.Validation("workflow.invalid", "Quy trình không hợp lệ.", errors);
        }

        try
        {
            return new WorkflowDefinition(
                planApproval,
                diffApproval,
                dto.PlannerMaxAttempts,
                dto.MaxFixIterations,
                severity,
                dto.RequireRegressionTest,
                new PullRequestSettings(
                    dto.PullRequest?.Draft ?? false,
                    dto.PullRequest?.Labels ?? [],
                    dto.PullRequest?.TitlePrefix ?? string.Empty,
                    dto.PullRequest?.Reviewers ?? []),
                notify,
                new StepModelOverrides(dto.Models?.Planner, dto.Models?.Coding, dto.Models?.Reviewer));
        }
        catch (DomainException ex)
        {
            return Error.Validation(ex.Code, ex.Message, new Dictionary<string, string[]> { [FieldOf(ex.Code)] = [ex.Message] });
        }
    }

    private static string FieldOf(string code) => code switch
    {
        "workflow.planner_attempts_invalid" => "plannerMaxAttempts",
        "workflow.fix_iterations_invalid" => "maxFixIterations",
        "workflow.reviewer_severity_invalid" => "reviewerBlockingSeverity",
        "workflow.model_invalid" => "models",
        _ when code.StartsWith("workflow.pr_", StringComparison.Ordinal) || code.StartsWith("pr_", StringComparison.Ordinal) => "pullRequest",
        _ => "definition",
    };
}

// ---------- Xem quy trình ----------

public sealed record GetRepositoryWorkflowQuery(Guid RepositoryId);

internal sealed class GetRepositoryWorkflowHandler(ProjectAccess access, ICodeRepositoryRepository repositories, IOptions<AgentLimitsOptions> limits)
    : IQueryHandler<GetRepositoryWorkflowQuery, RepositoryWorkflowDto>
{
    public async Task<Result<RepositoryWorkflowDto>> HandleAsync(GetRepositoryWorkflowQuery query, CancellationToken cancellationToken)
    {
        var repository = await repositories.GetByIdAsync(new RepositoryId(query.RepositoryId), cancellationToken);
        if (repository is null)
        {
            return Error.NotFound("repository", query.RepositoryId);
        }

        var user = await access.RequireAsync(repository.ProjectId, ProjectPermission.ViewTasks, "repository.workflow", repository.Id.ToString(), cancellationToken);
        if (user.IsFailure)
        {
            return user.Error!;
        }

        return new RepositoryWorkflowDto(
            repository.Id.Value,
            repository.FullName,
            repository.WorkflowVersion,
            repository.WorkflowVersion == 0,
            WorkflowMapping.ToDto(repository.Workflow),
            WorkflowMapping.Limits(limits.Value));
    }
}

// ---------- Lịch sử ----------

public sealed record ListWorkflowRevisionsQuery(Guid RepositoryId, int? Limit);

internal sealed class ListWorkflowRevisionsHandler(ProjectAccess access, ICodeRepositoryRepository repositories, IWorkflowRevisionRepository revisions)
    : IQueryHandler<ListWorkflowRevisionsQuery, IReadOnlyList<WorkflowRevisionDto>>
{
    public async Task<Result<IReadOnlyList<WorkflowRevisionDto>>> HandleAsync(ListWorkflowRevisionsQuery query, CancellationToken cancellationToken)
    {
        var repository = await repositories.GetByIdAsync(new RepositoryId(query.RepositoryId), cancellationToken);
        if (repository is null)
        {
            return Error.NotFound("repository", query.RepositoryId);
        }

        var user = await access.RequireAsync(repository.ProjectId, ProjectPermission.ViewTasks, "repository.workflow", repository.Id.ToString(), cancellationToken);
        if (user.IsFailure)
        {
            return user.Error!;
        }

        var items = await revisions.ListByRepositoryAsync(repository.Id, Math.Clamp(query.Limit ?? 50, 1, 200), cancellationToken);
        return Result<IReadOnlyList<WorkflowRevisionDto>>.Success(items
            .Select(r => new WorkflowRevisionDto(r.Version, r.ChangedBy.Value, r.ChangedAt, r.Comment, WorkflowMapping.ToDto(r.Definition)))
            .ToArray());
    }
}

// ---------- Sửa quy trình ----------

/// <param name="ExpectedVersion">Phiên bản người sửa đang xem; khác phiên bản hiện hành thì trả 409 để tránh ghi đè.</param>
public sealed record UpdateRepositoryWorkflowCommand(Guid RepositoryId, int ExpectedVersion, WorkflowDefinitionDto Definition, string? Comment);

internal sealed class UpdateRepositoryWorkflowHandler(
    ProjectAccess access,
    ICodeRepositoryRepository repositories,
    IWorkflowRevisionRepository revisions,
    IAuditLog auditLog,
    IUnitOfWork unitOfWork,
    IOptions<AgentLimitsOptions> limits,
    TimeProvider clock) : ICommandHandler<UpdateRepositoryWorkflowCommand, RepositoryWorkflowDto>
{
    public async Task<Result<RepositoryWorkflowDto>> HandleAsync(UpdateRepositoryWorkflowCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.Definition is null)
        {
            return Error.Validation("workflow.required", "Thiếu nội dung quy trình.");
        }

        var repository = await repositories.GetByIdAsync(new RepositoryId(command.RepositoryId), cancellationToken);
        if (repository is null)
        {
            return Error.NotFound("repository", command.RepositoryId);
        }

        var user = await access.RequireAsync(repository.ProjectId, ProjectPermission.ManageRepositories, "repository.workflow", repository.Id.ToString(), cancellationToken);
        if (user.IsFailure)
        {
            return user.Error!;
        }

        var definition = WorkflowMapping.FromDto(command.Definition, Math.Min(WorkflowDefinition.MaxFixIterationsLimit, limits.Value.MaxFixIterations));
        if (definition.IsFailure)
        {
            return definition.Error!;
        }

        if (command.ExpectedVersion != repository.WorkflowVersion)
        {
            return Error.Conflict(
                "workflow.version_conflict",
                $"Quy trình đã được đổi sang phiên bản {repository.WorkflowVersion}. Tải lại rồi áp dụng thay đổi của bạn.");
        }

        if (definition.Value == repository.Workflow)
        {
            return Error.Validation("workflow.unchanged", "Quy trình không có thay đổi nào.");
        }

        var now = clock.GetUtcNow();
        var revision = repository.UpdateWorkflow(definition.Value, command.ExpectedVersion, user.Value, command.Comment, now);
        revisions.Add(revision);
        auditLog.Add(AuditEntry.Record(
            Actor.ForUser(user.Value),
            "repository.workflow.update",
            "repository",
            repository.Id.ToString(),
            AuditOutcome.Allowed,
            now,
            repository.ProjectId,
            reason: $"v{revision.Version}: {command.Comment}"));

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new RepositoryWorkflowDto(
            repository.Id.Value,
            repository.FullName,
            repository.WorkflowVersion,
            IsDefault: false,
            WorkflowMapping.ToDto(repository.Workflow),
            WorkflowMapping.Limits(limits.Value));
    }
}
