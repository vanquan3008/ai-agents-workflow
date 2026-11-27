using DevAgent.Domain.Common;
using DevAgent.Domain.Policies;
using DevAgent.Domain.Repositories;
using DevAgent.Domain.Workflows;
using Shouldly;

namespace DevAgent.Domain.Tests;

public sealed class WorkflowDefinitionTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 26, 8, 0, 0, TimeSpan.Zero);
    private static readonly PolicyDecision Auto = new(PolicyOutcome.AutoApproved, SensitiveArea.None, 1, ["ok"]);
    private static readonly PolicyDecision Sensitive = new(PolicyOutcome.RequiresApproval, SensitiveArea.Migration, 1, ["migration"]);

    private static WorkflowDefinition Build(
        ApprovalRequirement plan = ApprovalRequirement.OnSensitive,
        int plannerAttempts = 3,
        int fix = 3,
        FindingSeverity severity = FindingSeverity.High,
        NotificationEvent[]? notify = null,
        string? plannerModel = null,
        string[]? reviewers = null) =>
        new(plan, ApprovalRequirement.OnSensitive, plannerAttempts, fix, severity, true,
            new PullRequestSettings(false, ["devagent"], "[DevAgent]", reviewers ?? []),
            notify ?? [], new StepModelOverrides(plannerModel, null, null));

    [Fact]
    public void Sensitive_changes_always_require_approval_even_with_on_sensitive()
    {
        var workflow = Build(ApprovalRequirement.OnSensitive);

        workflow.RequiresPlanApproval(Sensitive).ShouldBeTrue();
        workflow.RequiresPlanApproval(Auto).ShouldBeFalse();
    }

    [Fact]
    public void Always_requires_approval_for_auto_scope_too()
    {
        Build(ApprovalRequirement.Always).RequiresPlanApproval(Auto).ShouldBeTrue();
    }

    [Fact]
    public void Mandatory_notifications_cannot_be_turned_off()
    {
        var workflow = Build(notify: []);

        workflow.ShouldNotify(NotificationEvent.ApprovalRequired).ShouldBeTrue();
        workflow.ShouldNotify(NotificationEvent.Completed).ShouldBeTrue();
        workflow.ShouldNotify(NotificationEvent.Failed).ShouldBeTrue();
        workflow.ShouldNotify(NotificationEvent.TaskAccepted).ShouldBeFalse();
    }

    [Fact]
    public void Reviewer_threshold_cannot_be_loosened_to_critical()
    {
        Should.Throw<DomainException>(() => Build(severity: FindingSeverity.Critical)).Code.ShouldBe("workflow.reviewer_severity_invalid");
        Build(severity: FindingSeverity.Medium).BlocksOn(FindingSeverity.Medium).ShouldBeTrue();
    }

    [Theory]
    [InlineData(0, 3)]
    [InlineData(6, 3)]
    [InlineData(3, -1)]
    [InlineData(3, 11)]
    public void Attempts_and_iterations_are_bounded(int plannerAttempts, int fix) =>
        Should.Throw<DomainException>(() => Build(plannerAttempts: plannerAttempts, fix: fix));

    [Fact]
    public void Effective_fix_iterations_never_exceed_system_cap() =>
        Build(fix: 8).EffectiveFixIterations(systemCap: 3).ShouldBe(3);

    [Theory]
    [InlineData("Claude Opus")]
    [InlineData("../evil")]
    public void Model_override_must_be_a_model_id(string model) =>
        Should.Throw<DomainException>(() => Build(plannerModel: model)).Code.ShouldBe("workflow.model_invalid");

    [Fact]
    public void Reviewer_login_is_validated() =>
        Should.Throw<DomainException>(() => Build(reviewers: ["not a login"]));

    [Fact]
    public void Value_equality_compares_content()
    {
        Build().ShouldBe(Build());
        Build(fix: 2).ShouldNotBe(Build(fix: 3));
    }

    [Fact]
    public void Repository_workflow_update_is_versioned_and_optimistic()
    {
        var repo = CodeRepository.Register(ProjectId.New(), 1, "acme", "app", "main", RepositoryPolicy.CreateDefault("b", "t"), Now);
        repo.WorkflowVersion.ShouldBe(0);
        repo.Workflow.ShouldBe(WorkflowDefinition.Default);

        var revision = repo.UpdateWorkflow(Build(ApprovalRequirement.Always), expectedVersion: 0, UserId.New(), "siết duyệt", Now);

        revision.Version.ShouldBe(1);
        repo.WorkflowVersion.ShouldBe(1);
        repo.Workflow.PlanApproval.ShouldBe(ApprovalRequirement.Always);
        Should.Throw<DomainException>(() => repo.UpdateWorkflow(Build(fix: 1), expectedVersion: 0, UserId.New(), null, Now))
            .Code.ShouldBe("workflow.version_conflict");
        Should.Throw<DomainException>(() => repo.UpdateWorkflow(Build(ApprovalRequirement.Always), expectedVersion: 1, UserId.New(), null, Now))
            .Code.ShouldBe("workflow.unchanged");
    }
}
