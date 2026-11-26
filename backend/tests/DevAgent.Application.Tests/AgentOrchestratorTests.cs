using System.Text.Json;
using DevAgent.Application.Abstractions.Ai;
using DevAgent.Application.Common;
using DevAgent.Application.Features.Approvals;
using DevAgent.Application.Orchestration;
using DevAgent.Application.Orchestration.Agents;
using DevAgent.Application.Tests.Fakes;
using DevAgent.Domain.Approvals;
using DevAgent.Domain.Common;
using DevAgent.Domain.Executions;
using DevAgent.Domain.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace DevAgent.Application.Tests;

public sealed class AgentOrchestratorTests
{
    private static readonly string[] PlanSteps = ["viết test", "sửa"];
    private static readonly string[] PlanCriteria = ["test hồi quy đạt"];
    private readonly TestFixture _fx = new();

    private static string Plan(params string[] paths) => JsonSerializer.Serialize(new
    {
        needs_clarification = false,
        summary = "Sửa bộ lọc",
        steps = PlanSteps,
        acceptance_criteria = PlanCriteria,
        risks = Array.Empty<string>(),
        affected_paths = paths,
    });

    private AgentOrchestrator Orchestrator(IModelClient model) => new(
        _fx.Store, _fx.Store, _fx.Store, _fx.Store, _fx.Store, _fx.Store,
        new FixedSourceControl(),
        new PlannerAgent(model),
        MsOptions.Create(_fx.Limits),
        _fx.Clock,
        NullLogger<AgentOrchestrator>.Instance);

    private AgentTask NewTask()
    {
        var task = AgentTask.Create(
            _fx.Project.Id, _fx.Repository.Id, "Bug", "Mô tả", new TaskSource(TaskChannel.Dashboard, null),
            new IdempotencyKey(Guid.NewGuid().ToString("N")), _fx.Requester.Id, _fx.Clock.GetUtcNow());
        _fx.Store.Tasks.Add(task);
        return task;
    }

    [Fact]
    public async Task Pending_task_moves_to_planning_with_base_commit_and_branch()
    {
        var task = NewTask();

        await Orchestrator(new ScriptedModelClient(Plan("src/a.cs"))).AdvanceAsync(task.Id, "w1", CancellationToken.None);

        task.Status.ShouldBe(AgentTaskStatus.Planning);
        task.BaseCommitSha.ShouldBe(FixedSourceControl.HeadSha);
        task.WorkBranch.ShouldStartWith("devagent/task-");
        _fx.Store.Jobs.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Safe_plan_proceeds_automatically_and_records_usage()
    {
        var task = NewTask();
        var orchestrator = Orchestrator(new ScriptedModelClient(Plan("src/Tasks/TaskFilter.cs", "tests/Tasks/TaskFilterTests.cs")));
        await orchestrator.AdvanceAsync(task.Id, "w1", CancellationToken.None);

        await orchestrator.AdvanceAsync(task.Id, "w1", CancellationToken.None);

        task.Status.ShouldBe(AgentTaskStatus.InProgress);
        task.CurrentPlan!.PolicyOutcome.ShouldBe(Domain.Policies.PolicyOutcome.AutoApproved);
        var execution = _fx.Store.Executions.ShouldHaveSingleItem();
        execution.Step.ShouldBe(AgentStep.Planner);
        execution.Status.ShouldBe(ExecutionStatus.Succeeded);
        execution.Usage.EstimatedCostUsd.ShouldBe(0.01m);
        _fx.Store.Approvals.ShouldBeEmpty();
    }

    [Fact]
    public async Task Sensitive_plan_waits_for_approval_bound_to_plan_version()
    {
        var task = NewTask();
        var orchestrator = Orchestrator(new ScriptedModelClient(Plan("src/Auth/LoginService.cs")));
        await orchestrator.AdvanceAsync(task.Id, "w1", CancellationToken.None);

        await orchestrator.AdvanceAsync(task.Id, "w1", CancellationToken.None);

        task.Status.ShouldBe(AgentTaskStatus.WaitingForApproval);
        var approval = _fx.Store.Approvals.ShouldHaveSingleItem();
        approval.Kind.ShouldBe(ApprovalKind.Plan);
        approval.PlanVersion.ShouldBe(1);
        approval.Status.ShouldBe(ApprovalStatus.Pending);
    }

    [Fact]
    public async Task Requester_cannot_approve_but_owner_can()
    {
        var task = NewTask();
        var orchestrator = Orchestrator(new ScriptedModelClient(Plan("src/Auth/LoginService.cs")));
        await orchestrator.AdvanceAsync(task.Id, "w1", CancellationToken.None);
        await orchestrator.AdvanceAsync(task.Id, "w1", CancellationToken.None);
        var approval = _fx.Store.Approvals.Single();
        var handler = new DecideApprovalHandler(_fx.Access, _fx.Store, _fx.Store, _fx.Store, _fx.Store, _fx.Store, _fx.Clock);

        var denied = await handler.HandleAsync(new DecideApprovalCommand(approval.Id.Value, ApprovalDecision.Approve, null), CancellationToken.None);
        denied.Error!.Type.ShouldBe(ErrorType.Forbidden);

        _fx.ActAs(_fx.Owner);
        var approved = await handler.HandleAsync(new DecideApprovalCommand(approval.Id.Value, ApprovalDecision.Approve, "ok"), CancellationToken.None);

        approved.IsSuccess.ShouldBeTrue();
        task.Status.ShouldBe(AgentTaskStatus.InProgress);
        approval.DecidedBy.ShouldBe(_fx.Owner.Id);
    }

    [Fact]
    public async Task Worker_without_user_cannot_approve()
    {
        _fx.ActAsNobody();
        var handler = new DecideApprovalHandler(_fx.Access, _fx.Store, _fx.Store, _fx.Store, _fx.Store, _fx.Store, _fx.Clock);
        var task = NewTask();
        task.StartPlanning(FixedSourceControl.HeadSha, "b", Actor.System, _fx.Clock.GetUtcNow());
        var plan = task.AttachPlan("s", ["a"], ["c"], [], ["src/Auth/x.cs"],
            new Domain.Policies.PolicyDecision(Domain.Policies.PolicyOutcome.RequiresApproval, Domain.Policies.SensitiveArea.Authentication, 1, ["auth"]),
            _fx.Clock.GetUtcNow());
        task.WaitForApproval("auth", Actor.System, _fx.Clock.GetUtcNow());
        var approval = Approval.ForPlan(task.Id, plan.Version, new(Domain.Policies.PolicyOutcome.RequiresApproval, Domain.Policies.SensitiveArea.Authentication, 1, ["auth"]), _fx.Clock.GetUtcNow(), TimeSpan.FromHours(1));
        _fx.Store.Approvals.Add(approval);

        var result = await handler.HandleAsync(new DecideApprovalCommand(approval.Id.Value, ApprovalDecision.Approve, null), CancellationToken.None);

        result.Error!.Type.ShouldBe(ErrorType.Unauthorized);
        approval.Status.ShouldBe(ApprovalStatus.Pending);
    }

    [Fact]
    public async Task Budget_exhausted_blocks_before_calling_model()
    {
        var task = NewTask();
        var model = new ScriptedModelClient(Plan("src/a.cs"));
        var orchestrator = Orchestrator(model);
        await orchestrator.AdvanceAsync(task.Id, "w1", CancellationToken.None);

        var spent = AgentExecution.Start(task.Id, AgentStep.Coding, 1, null, "m", "w0", _fx.Clock.GetUtcNow());
        spent.RecordUsage(new TokenUsage(1, 1, 0, _fx.Limits.MaxCostUsdPerTask));
        _fx.Store.Executions.Add(spent);

        await orchestrator.AdvanceAsync(task.Id, "w1", CancellationToken.None);

        task.Status.ShouldBe(AgentTaskStatus.Blocked);
        task.StatusReason!.ShouldContain("ngân sách");
        model.Calls.ShouldBe(0);
    }

    [Fact]
    public async Task Timed_out_task_stops()
    {
        var task = NewTask();
        var orchestrator = Orchestrator(new ScriptedModelClient(Plan("src/a.cs")));
        await orchestrator.AdvanceAsync(task.Id, "w1", CancellationToken.None);

        _fx.Clock.Advance(TimeSpan.FromMinutes(_fx.Limits.TaskTimeoutMinutes + 1));
        await orchestrator.AdvanceAsync(task.Id, "w1", CancellationToken.None);

        task.Status.ShouldBe(AgentTaskStatus.TimedOut);
    }

    [Fact]
    public async Task Clarification_request_moves_task_to_needs_clarification()
    {
        var task = NewTask();
        var clarify = JsonSerializer.Serialize(new { needs_clarification = true, clarification_question = "Bước tái hiện?" });
        var orchestrator = Orchestrator(new ScriptedModelClient(clarify));
        await orchestrator.AdvanceAsync(task.Id, "w1", CancellationToken.None);

        await orchestrator.AdvanceAsync(task.Id, "w1", CancellationToken.None);

        task.Status.ShouldBe(AgentTaskStatus.NeedsClarification);
        task.StatusReason.ShouldBe("Bước tái hiện?");
    }

    [Fact]
    public async Task Refusal_blocks_task_for_human_review()
    {
        var task = NewTask();
        var orchestrator = Orchestrator(new ScriptedModelClient("") { StopReason = ModelStopReason.Refusal });
        await orchestrator.AdvanceAsync(task.Id, "w1", CancellationToken.None);

        await orchestrator.AdvanceAsync(task.Id, "w1", CancellationToken.None);

        task.Status.ShouldBe(AgentTaskStatus.Blocked);
        _fx.Store.Executions.Single().Status.ShouldBe(ExecutionStatus.Failed);
    }

    [Fact]
    public async Task Cancelled_task_is_not_processed()
    {
        var task = NewTask();
        task.Cancel(_fx.Requester.Id, null, _fx.Clock.GetUtcNow());
        var model = new ScriptedModelClient(Plan("src/a.cs"));

        await Orchestrator(model).AdvanceAsync(task.Id, "w1", CancellationToken.None);

        task.Status.ShouldBe(AgentTaskStatus.Cancelled);
        model.Calls.ShouldBe(0);
    }
}

public sealed class PlannerParsingTests
{
    [Fact]
    public void Parses_plan_wrapped_in_prose()
    {
        var (output, error) = PlannerAgent.Parse("Đây là kế hoạch:\n{\"needs_clarification\":false,\"summary\":\"s\",\"steps\":[\"a\"],\"affected_paths\":[\"src/x.cs\"]}\nHết.");

        error.ShouldBeNull();
        output!.AffectedPaths.ShouldBe(["src/x.cs"]);
    }

    [Theory]
    [InlineData("không có json")]
    [InlineData("{\"needs_clarification\":false,\"summary\":\"s\",\"steps\":[]}")]
    [InlineData("{\"needs_clarification\":true}")]
    [InlineData("{broken json}")]
    public void Rejects_incomplete_or_invalid_plans(string text)
    {
        var (output, error) = PlannerAgent.Parse(text);

        output.ShouldBeNull();
        error.ShouldNotBeNullOrWhiteSpace();
    }
}

public sealed class ApiNamesTests
{
    [Theory]
    [InlineData(AgentTaskStatus.WaitingForApproval, "WAITING_FOR_APPROVAL")]
    [InlineData(AgentTaskStatus.Pending, "PENDING")]
    [InlineData(AgentTaskStatus.TimedOut, "TIMED_OUT")]
    public void Status_round_trips(AgentTaskStatus status, string api)
    {
        ApiNames.ToApi(status).ShouldBe(api);
        ApiNames.TryParse<AgentTaskStatus>(api, out var parsed).ShouldBeTrue();
        parsed.ShouldBe(status);
    }

    [Fact]
    public void GitHub_is_one_word() => ApiNames.ToApi(TaskChannel.GitHubIssue).ShouldBe("GITHUB_ISSUE");
}
