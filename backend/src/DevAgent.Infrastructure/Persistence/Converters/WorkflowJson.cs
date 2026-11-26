using System.Text.Json;
using System.Text.Json.Serialization;
using DevAgent.Domain.Workflows;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace DevAgent.Infrastructure.Persistence.Converters;

/// <summary>
/// Lưu <see cref="WorkflowDefinition"/> dạng jsonb. Dùng snapshot riêng (enum dạng chuỗi) để đổi cấu trúc domain
/// không làm hỏng dữ liệu cũ; trường thiếu trong JSON cũ lấy giá trị mặc định.
/// </summary>
internal static class WorkflowJson
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public static string Serialize(WorkflowDefinition definition) => JsonSerializer.Serialize(Snapshot.From(definition), Options);

    public static WorkflowDefinition Deserialize(string json) => JsonSerializer.Deserialize<Snapshot>(json, Options)!.ToDefinition();

    public static ValueConverter<WorkflowDefinition, string> Converter { get; } = new(d => Serialize(d), s => Deserialize(s));

    // EF không gọi converter với null (cột NULL ánh xạ trực tiếp sang null), nên chỉ khác ở chú thích nullability.
    public static ValueConverter<WorkflowDefinition?, string> NullableConverter { get; } = new(d => Serialize(d!), s => Deserialize(s));

    public static ValueComparer<WorkflowDefinition?> NullableComparer { get; } = new(
        (a, b) => (a == null && b == null) || (a != null && b != null && a.Equals(b)),
        d => d == null ? 0 : d.GetHashCode(),
        d => d);

    public static ValueComparer<WorkflowDefinition> Comparer { get; } = new(
        (a, b) => (a == null && b == null) || (a != null && b != null && a.Equals(b)),
        d => d.GetHashCode(),
        d => d);

    private sealed record Snapshot
    {
        public ApprovalRequirement PlanApproval { get; init; } = ApprovalRequirement.OnSensitive;

        public ApprovalRequirement DiffApproval { get; init; } = ApprovalRequirement.OnSensitive;

        public int PlannerMaxAttempts { get; init; } = 3;

        public int MaxFixIterations { get; init; } = 3;

        public FindingSeverity ReviewerBlockingSeverity { get; init; } = FindingSeverity.High;

        public bool RequireRegressionTest { get; init; } = true;

        public bool PrDraft { get; init; }

        // Mặc định trùng WorkflowDefinition.Default để JSON thiếu trường (hoặc "{}") đọc ra quy trình mặc định.
        public string[] PrLabels { get; init; } = ["devagent"];

        public string PrTitlePrefix { get; init; } = "[DevAgent]";

        public string[] PrReviewers { get; init; } = [];

        public NotificationEvent[] NotifyOn { get; init; } = Enum.GetValues<NotificationEvent>();

        public string? PlannerModel { get; init; }

        public string? CodingModel { get; init; }

        public string? ReviewerModel { get; init; }

        public static Snapshot From(WorkflowDefinition d) => new()
        {
            PlanApproval = d.PlanApproval,
            DiffApproval = d.DiffApproval,
            PlannerMaxAttempts = d.PlannerMaxAttempts,
            MaxFixIterations = d.MaxFixIterations,
            ReviewerBlockingSeverity = d.ReviewerBlockingSeverity,
            RequireRegressionTest = d.RequireRegressionTest,
            PrDraft = d.PullRequest.Draft,
            PrLabels = [.. d.PullRequest.Labels],
            PrTitlePrefix = d.PullRequest.TitlePrefix,
            PrReviewers = [.. d.PullRequest.Reviewers],
            NotifyOn = [.. d.NotifyOn],
            PlannerModel = d.Models.Planner,
            CodingModel = d.Models.Coding,
            ReviewerModel = d.Models.Reviewer,
        };

        public WorkflowDefinition ToDefinition() => new(
            PlanApproval,
            DiffApproval,
            PlannerMaxAttempts,
            MaxFixIterations,
            ReviewerBlockingSeverity,
            RequireRegressionTest,
            new PullRequestSettings(PrDraft, PrLabels, PrTitlePrefix, PrReviewers),
            NotifyOn,
            new StepModelOverrides(PlannerModel, CodingModel, ReviewerModel));
    }
}
