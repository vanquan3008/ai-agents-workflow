using DevAgent.Application.Common;
using DevAgent.Application.Options;
using DevAgent.Domain.Common;
using DevAgent.Domain.Repositories;
using DevAgent.Domain.Workflows;

namespace DevAgent.Application.Features.Workflows;

internal static class WorkflowMapping
{
    /// <summary>Rules the system always enforces; they cannot be configured through a workflow.</summary>
    public static readonly IReadOnlyList<string> Invariants =
    [
        "Các bước Planner → Coding → Testing → Reviewer → Publisher luôn chạy theo thứ tự.",
        "Thay đổi chạm vùng nhạy cảm (xác thực, phân quyền, migration, credential, CI/CD, xóa file) luôn cần người duyệt.",
        "Agent không thể tự duyệt; quyết định duyệt gắn với người duyệt, phiên bản kế hoạch hoặc commit.",
        "Build/test lỗi hoặc còn phát hiện chặn thì không xuất bản PR.",
        "Con người merge PR trên GitHub; không triển khai production.",
        "Lệnh chỉ chạy trong sandbox; ngân sách, timeout và số task đồng thời theo cấu hình hệ thống.",
    ];

    public static WorkflowDefinitionDto ToDto(WorkflowDefinition definition) => new(
        ApiNames.ToApi(definition.PlanApproval),
        ApiNames.ToApi(definition.DiffApproval),
        definition.PlannerMaxAttempts,
        definition.MaxFixIterations,
        ApiNames.ToApi(definition.ReviewerBlockingSeverity),
        definition.RequireRegressionTest,
        new PullRequestSettingsDto(definition.PullRequest.Draft, definition.PullRequest.Labels, definition.PullRequest.TitlePrefix, definition.PullRequest.Reviewers),
        definition.NotifyOn.Select(e => ApiNames.ToApi(e)).ToArray(),
        new ModelOverridesDto(definition.Models.Planner, definition.Models.Coding, definition.Models.Reviewer));

    public static WorkflowLimitsDto Limits(AgentLimitsOptions limits) => new(
        WorkflowDefinition.MaxPlannerAttempts,
        FixIterationsCap(limits),
        Enum.GetValues<ApprovalRequirement>().Select(v => ApiNames.ToApi(v)).ToArray(),
        Enum.GetValues<FindingSeverity>().Where(v => v <= FindingSeverity.High).Select(v => ApiNames.ToApi(v)).ToArray(),
        Enum.GetValues<NotificationEvent>().Select(v => ApiNames.ToApi(v)).ToArray(),
        WorkflowDefinition.MandatoryNotifications.Select(v => ApiNames.ToApi(v)).ToArray(),
        Invariants);

    public static int FixIterationsCap(AgentLimitsOptions limits) =>
        Math.Min(WorkflowDefinition.MaxFixIterationsLimit, limits.MaxFixIterations);

    public static RepositoryWorkflowDto ToRepositoryWorkflow(CodeRepository repository, AgentLimitsOptions limits) => new(
        repository.Id.Value,
        repository.FullName,
        repository.WorkflowVersion,
        repository.WorkflowVersion == 0,
        ToDto(repository.Workflow),
        Limits(limits));

    /// <summary>Converts the DTO into the value object, collecting per-field errors for a detailed 400 response.</summary>
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
            if (ApiNames.TryParse<NotificationEvent>(value, out var notificationEvent))
            {
                notify.Add(notificationEvent);
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
            return Error.Field(ex.Code, FieldOf(ex.Code), ex.Message);
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
