using DevAgent.Application.Common;
using DevAgent.Domain.Approvals;
using DevAgent.Domain.Tasks;

namespace DevAgent.Application.Features.Approvals;

public sealed record ApprovalDto(
    Guid Id,
    Guid TaskId,
    Guid ProjectId,
    string TaskTitle,
    string Kind,
    string Status,
    int? PlanVersion,
    string? CommitSha,
    IReadOnlyList<string> SensitiveAreas,
    int PolicyVersion,
    string Scope,
    DateTimeOffset RequestedAt,
    DateTimeOffset ExpiresAt,
    Guid? DecidedBy,
    DateTimeOffset? DecidedAt,
    string? Comment)
{
    public static ApprovalDto From(Approval approval, AgentTask task)
    {
        ArgumentNullException.ThrowIfNull(approval);
        ArgumentNullException.ThrowIfNull(task);
        return new ApprovalDto(
            approval.Id.Value,
            task.Id.Value,
            task.ProjectId.Value,
            task.Title,
            ApiNames.ToApi(approval.Kind),
            ApiNames.ToApi(approval.Status),
            approval.PlanVersion,
            approval.CommitSha,
            ApiNames.FlagsToApi(approval.SensitiveAreas),
            approval.PolicyVersion,
            approval.Scope,
            approval.RequestedAt,
            approval.ExpiresAt,
            approval.DecidedBy?.Value,
            approval.DecidedAt,
            approval.Comment);
    }
}
