using DevAgent.Application.Features.Tasks;

namespace DevAgent.Application.Features.Executions;

public sealed record ExecutionSummaryDto(
    Guid Id,
    Guid TaskId,
    string Step,
    int Attempt,
    string Status,
    string Model,
    UsageDto Usage,
    string? Error,
    DateTimeOffset StartedAt,
    DateTimeOffset? FinishedAt,
    double? DurationSeconds);

public sealed record ToolExecutionDto(
    Guid Id,
    int Sequence,
    string ToolName,
    string Status,
    int? ExitCode,
    string InputJson,
    string? OutputJson,
    string? DenialReason,
    DateTimeOffset StartedAt,
    DateTimeOffset? FinishedAt);

public sealed record ExecutionDetailDto(ExecutionSummaryDto Execution, string? Checkpoint, IReadOnlyList<ToolExecutionDto> ToolExecutions);
