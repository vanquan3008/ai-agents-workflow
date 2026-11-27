using DevAgent.Domain.Common;
using DevAgent.Domain.Users;
using DevAgent.Domain.Workflows;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace DevAgent.Infrastructure.Persistence.Converters;

// Strongly typed id <-> uuid mappings, registered once in ConfigureConventions.
internal sealed class UserIdConverter() : ValueConverter<UserId, Guid>(v => v.Value, v => new UserId(v));

internal sealed class ProjectIdConverter() : ValueConverter<ProjectId, Guid>(v => v.Value, v => new ProjectId(v));

internal sealed class RepositoryIdConverter() : ValueConverter<RepositoryId, Guid>(v => v.Value, v => new RepositoryId(v));

internal sealed class TaskIdConverter() : ValueConverter<TaskId, Guid>(v => v.Value, v => new TaskId(v));

internal sealed class TaskPlanIdConverter() : ValueConverter<TaskPlanId, Guid>(v => v.Value, v => new TaskPlanId(v));

internal sealed class ExecutionIdConverter() : ValueConverter<ExecutionId, Guid>(v => v.Value, v => new ExecutionId(v));

internal sealed class ToolExecutionIdConverter() : ValueConverter<ToolExecutionId, Guid>(v => v.Value, v => new ToolExecutionId(v));

internal sealed class ApprovalIdConverter() : ValueConverter<ApprovalId, Guid>(v => v.Value, v => new ApprovalId(v));

internal sealed class AgentProfileIdConverter() : ValueConverter<AgentProfileId, Guid>(v => v.Value, v => new AgentProfileId(v));

internal sealed class AgentInstanceIdConverter() : ValueConverter<AgentInstanceId, Guid>(v => v.Value, v => new AgentInstanceId(v));

internal sealed class AuditEntryIdConverter() : ValueConverter<AuditEntryId, Guid>(v => v.Value, v => new AuditEntryId(v));

internal sealed class WorkflowRevisionIdConverter() : ValueConverter<WorkflowRevisionId, Guid>(v => v.Value, v => new WorkflowRevisionId(v));

internal sealed class RefreshTokenIdConverter() : ValueConverter<RefreshTokenId, Guid>(v => v.Value, v => new RefreshTokenId(v));
