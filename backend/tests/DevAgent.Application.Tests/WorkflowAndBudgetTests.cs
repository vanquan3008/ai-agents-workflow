using System.Text.Json;
using DevAgent.Application.Abstractions.Ai;
using DevAgent.Application.Abstractions.Messaging;
using DevAgent.Application.AgentRuntime.Budget;
using DevAgent.Application.AgentRuntime.Jobs;
using DevAgent.Application.AgentRuntime.Orchestration;
using DevAgent.Application.AgentRuntime.Planner;
using DevAgent.Application.AgentRuntime.Steps;
using DevAgent.Application.Common;
using DevAgent.Application.Features.Notifications;
using DevAgent.Application.Features.Workflows;
using DevAgent.Application.Tests.Fakes;
using DevAgent.Domain.Common;
using DevAgent.Domain.Executions;
using DevAgent.Domain.Tasks;
using DevAgent.Domain.Workflows;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace DevAgent.Application.Tests;

public sealed class WorkflowAndBudgetTests
{
    private static readonly string[] Steps = ["viết test", "sửa"];
    private static readonly string[] Criteria = ["test hồi quy đạt"];
    private static readonly ModelPrice Paid = new(0m, 10m, 0m);
    private readonly TestFixture _fx = new();

    private static string Plan(params string[] paths) => JsonSerializer.Serialize(new
    {
        needs_clarification = false,
        summary = "Sửa bộ lọc",
        steps = Steps,
        acceptance_criteria = Criteria,
        risks = Array.Empty<string>(),
        affected_paths = paths,
    });

    private AgentOrchestrator Orchestrator(IModelClient model)
    {
        var limits = MsOptions.Create(_fx.Limits);
        var source = new FixedSourceControl();
        IWorkflowStep[] steps =
        [
            new StartTaskStep(source, _fx.Store, _fx.Store, _fx.Clock),
            new PlanningStep(_fx.Store, _fx.Store, _fx.Store, _fx.Store, source, new PlannerAgent(model), new BudgetGuard(_fx.Store, model, limits), limits, _fx.Clock),
            new PendingImplementationStep(_fx.Store, _fx.Clock),
        ];
        return new AgentOrchestrator(_fx.Store, _fx.Store, _fx.Store, _fx.Store, steps, limits, _fx.Clock, NullLogger<AgentOrchestrator>.Instance);
    }

    private AgentTask NewTask()
    {
        var task = AgentTask.Create(
            _fx.Project.Id, _fx.Repository.Id, "Bug", "Mô tả", new TaskSource(TaskChannel.Dashboard, null),
            new IdempotencyKey(Guid.NewGuid().ToString("N")), _fx.Requester.Id, _fx.Clock.GetUtcNow());
        _fx.Store.Tasks.Add(task);
        return task;
    }

    private static WorkflowDefinition Workflow(
        ApprovalRequirement plan = ApprovalRequirement.OnSensitive,
        int plannerAttempts = 3,
        string? plannerModel = null) =>
        new(plan, ApprovalRequirement.OnSensitive, plannerAttempts, 3, FindingSeverity.High, true,
            new PullRequestSettings(false, [], "", []), Enum.GetValues<NotificationEvent>(), new StepModelOverrides(plannerModel, null, null));

    // ---------------- Budget ----------------

    [Fact]
    public async Task Output_cap_of_next_call_is_derived_from_remaining_budget()
    {
        _fx.Limits.MaxCostUsdPerTask = 0.05m; // 10 USD per 1M output tokens leaves room for 5,000 tokens
        var task = NewTask();
        var model = new ScriptedModelClient(Plan("src/Tasks/A.cs", "tests/Tasks/ATests.cs")) { Price = _ => Paid };
        var orchestrator = Orchestrator(model);
        await orchestrator.AdvanceAsync(task.Id, "w", CancellationToken.None);

        await orchestrator.AdvanceAsync(task.Id, "w", CancellationToken.None);

        model.LastRequest!.MaxOutputTokens.ShouldBe(5_000);
    }

    [Fact]
    public async Task Call_is_refused_when_remaining_budget_cannot_cover_minimum_output()
    {
        _fx.Limits.MaxCostUsdPerTask = 0.05m;
        var task = NewTask();
        var model = new ScriptedModelClient(Plan("src/a.cs")) { Price = _ => Paid };
        var orchestrator = Orchestrator(model);
        await orchestrator.AdvanceAsync(task.Id, "w", CancellationToken.None);
        var spent = AgentExecution.Start(task.Id, AgentStep.Planner, 1, null, "m", "w", _fx.Clock.GetUtcNow());
        spent.RecordModelCall(new TokenUsage(0, 0, 0, 0.045m)); // 0.005 USD left = 500 tokens < 1,024
        spent.Succeed(_fx.Clock.GetUtcNow());
        _fx.Store.Executions.Add(spent);

        await orchestrator.AdvanceAsync(task.Id, "w", CancellationToken.None);

        task.Status.ShouldBe(AgentTaskStatus.Blocked);
        task.StatusReason!.ShouldContain("ngân sách");
        model.Calls.ShouldBe(0);
    }

    [Fact]
    public async Task Max_model_calls_per_task_is_enforced()
    {
        _fx.Limits.MaxModelCallsPerTask = 1;
        var task = NewTask();
        var model = new ScriptedModelClient(Plan("src/a.cs"));
        var orchestrator = Orchestrator(model);
        await orchestrator.AdvanceAsync(task.Id, "w", CancellationToken.None);
        var used = AgentExecution.Start(task.Id, AgentStep.Planner, 1, null, "m", "w", _fx.Clock.GetUtcNow());
        used.RecordModelCall(new TokenUsage(1, 1, 0, 0m));
        used.Succeed(_fx.Clock.GetUtcNow());
        _fx.Store.Executions.Add(used);

        await orchestrator.AdvanceAsync(task.Id, "w", CancellationToken.None);

        task.Status.ShouldBe(AgentTaskStatus.Blocked);
        task.StatusReason!.ShouldContain("lần gọi model");
        model.Calls.ShouldBe(0);
    }

    [Fact]
    public async Task Model_without_price_is_refused_fail_closed()
    {
        var task = NewTask();
        var model = new ScriptedModelClient(Plan("src/a.cs")) { Price = _ => null };
        var orchestrator = Orchestrator(model);
        await orchestrator.AdvanceAsync(task.Id, "w", CancellationToken.None);

        await orchestrator.AdvanceAsync(task.Id, "w", CancellationToken.None);

        task.Status.ShouldBe(AgentTaskStatus.Blocked);
        task.StatusReason!.ShouldContain("đơn giá");
        model.Calls.ShouldBe(0);
    }

    [Fact]
    public async Task Model_calls_are_counted_on_execution()
    {
        var task = NewTask();
        var orchestrator = Orchestrator(new ScriptedModelClient(Plan("src/Tasks/A.cs")));
        await orchestrator.AdvanceAsync(task.Id, "w", CancellationToken.None);
        await orchestrator.AdvanceAsync(task.Id, "w", CancellationToken.None);

        _fx.Store.Executions.Single().ModelCalls.ShouldBe(1);
    }

    // ---------------- Planner retries per planning round ----------------

    [Fact]
    public async Task Planner_failures_from_previous_round_do_not_block_retry()
    {
        var task = NewTask();
        var model = new ScriptedModelClient("không phải json", "không phải json", "không phải json", Plan("src/Tasks/A.cs"));
        var orchestrator = Orchestrator(model);
        await orchestrator.AdvanceAsync(task.Id, "w", CancellationToken.None); // PENDING → PLANNING
        for (var i = 0; i < 3; i++)
        {
            await orchestrator.AdvanceAsync(task.Id, "w", CancellationToken.None);
        }

        await orchestrator.AdvanceAsync(task.Id, "w", CancellationToken.None);
        task.Status.ShouldBe(AgentTaskStatus.Failed);
        task.StatusReason!.ShouldContain("trong lượt lập kế hoạch này");

        _fx.Clock.Advance(TimeSpan.FromSeconds(1));
        task.Retry(_fx.Requester.Id, maxRetries: 2, _fx.Clock.GetUtcNow());
        await orchestrator.AdvanceAsync(task.Id, "w", CancellationToken.None); // PENDING → PLANNING (new round)
        await orchestrator.AdvanceAsync(task.Id, "w", CancellationToken.None);

        task.Status.ShouldBe(AgentTaskStatus.InProgress);
        var planner = _fx.Store.Executions.Where(e => e.Step == AgentStep.Planner).ToList();
        planner.Count.ShouldBe(4);                       // history is kept
        planner.Max(e => e.Attempt).ShouldBe(4);         // numbering continues
    }

    [Fact]
    public async Task Success_and_clarification_do_not_count_as_failures()
    {
        var task = NewTask();
        var clarify = JsonSerializer.Serialize(new { needs_clarification = true, clarification_question = "Bước tái hiện?" });
        var model = new ScriptedModelClient(clarify, clarify, clarify, Plan("src/Tasks/A.cs"));
        var orchestrator = Orchestrator(model);
        await orchestrator.AdvanceAsync(task.Id, "w", CancellationToken.None);

        for (var i = 0; i < 3; i++)
        {
            await orchestrator.AdvanceAsync(task.Id, "w", CancellationToken.None);
            task.Status.ShouldBe(AgentTaskStatus.NeedsClarification);
            _fx.Clock.Advance(TimeSpan.FromSeconds(1));
            task.ProvideClarification("thêm thông tin", _fx.Requester.Id, _fx.Clock.GetUtcNow());
        }

        await orchestrator.AdvanceAsync(task.Id, "w", CancellationToken.None);
        task.Status.ShouldBe(AgentTaskStatus.InProgress);
    }

    // ---------------- Repository workflows ----------------

    [Fact]
    public async Task Failed_model_request_still_consumes_call_quota()
    {
        _fx.Limits.MaxModelCallsPerTask = 1;
        var task = NewTask();
        var orchestrator = Orchestrator(new ScriptedModelClient());
        await orchestrator.AdvanceAsync(task.Id, "w", CancellationToken.None);

        await Should.ThrowAsync<IndexOutOfRangeException>(() => orchestrator.AdvanceAsync(task.Id, "w", CancellationToken.None));
        _fx.Store.Executions.Single().ModelCalls.ShouldBe(1);
        await orchestrator.AdvanceAsync(task.Id, "w", CancellationToken.None);

        task.Status.ShouldBe(AgentTaskStatus.Blocked);
        task.StatusReason!.ShouldContain("lần gọi model");
    }

    [Fact]
    public async Task Always_plan_approval_requires_approval_for_auto_scope_plan()
    {
        _fx.Repository.UpdateWorkflow(Workflow(ApprovalRequirement.Always), 0, _fx.Owner.Id, "siết", _fx.Clock.GetUtcNow());
        var task = NewTask();
        var orchestrator = Orchestrator(new ScriptedModelClient(Plan("src/Tasks/A.cs")));
        await orchestrator.AdvanceAsync(task.Id, "w", CancellationToken.None);

        await orchestrator.AdvanceAsync(task.Id, "w", CancellationToken.None);

        task.Status.ShouldBe(AgentTaskStatus.WaitingForApproval);
        task.StatusReason!.ShouldContain("luôn duyệt");
        _fx.Store.Approvals.ShouldHaveSingleItem().Scope.ShouldContain("luôn duyệt");
    }

    [Fact]
    public async Task Task_keeps_workflow_snapshot_when_repository_workflow_changes_mid_run()
    {
        var task = NewTask();
        var orchestrator = Orchestrator(new ScriptedModelClient(Plan("src/Tasks/A.cs")));
        await orchestrator.AdvanceAsync(task.Id, "w", CancellationToken.None); // snapshots the default workflow v0

        _fx.Repository.UpdateWorkflow(Workflow(ApprovalRequirement.Always), 0, _fx.Owner.Id, null, _fx.Clock.GetUtcNow());
        await orchestrator.AdvanceAsync(task.Id, "w", CancellationToken.None);

        task.WorkflowVersion.ShouldBe(0);
        task.Status.ShouldBe(AgentTaskStatus.InProgress);
    }

    [Fact]
    public async Task Planner_model_override_and_attempt_limit_come_from_workflow()
    {
        _fx.Repository.UpdateWorkflow(Workflow(plannerAttempts: 1, plannerModel: "claude-sonnet-5-5"), 0, _fx.Owner.Id, null, _fx.Clock.GetUtcNow());
        var task = NewTask();
        var model = new ScriptedModelClient("không phải json");
        var orchestrator = Orchestrator(model);
        await orchestrator.AdvanceAsync(task.Id, "w", CancellationToken.None);
        await orchestrator.AdvanceAsync(task.Id, "w", CancellationToken.None);

        model.LastRequest!.ModelOverride.ShouldBe("claude-sonnet-5-5");
        _fx.Store.Executions.Single().Model.ShouldBe("claude-sonnet-5-5");

        await orchestrator.AdvanceAsync(task.Id, "w", CancellationToken.None);
        task.Status.ShouldBe(AgentTaskStatus.Failed);
    }

    [Fact]
    public async Task Dead_letter_marks_task_failed_and_closes_running_execution()
    {
        var task = NewTask();
        task.StartPlanning(FixedSourceControl.HeadSha, "b", WorkflowDefinition.Default, 0, Actor.System, _fx.Clock.GetUtcNow());
        var running = AgentExecution.Start(task.Id, AgentStep.Planner, 1, null, "m", "w-dead", _fx.Clock.GetUtcNow());
        _fx.Store.Executions.Add(running);
        var handler = new ProcessTaskJobHandler(Orchestrator(new ScriptedModelClient("x")), new FixedWorker(), NullLogger<ProcessTaskJobHandler>.Instance);

        await handler.OnDeadLetterAsync(
            new DeadLetterJob(Guid.NewGuid(), JobTypes.ProcessTask, JsonSerializer.Serialize(new Features.Tasks.ProcessTaskPayload(task.Id.Value)), 5, 5, "Lease hết hạn"),
            CancellationToken.None);

        task.Status.ShouldBe(AgentTaskStatus.Failed);
        task.StatusReason!.ShouldContain("5/5");
        running.Status.ShouldBe(ExecutionStatus.Failed);
        running.Error!.ShouldContain("Lease hết hạn");
    }

    // ---------------- Notification outbox ----------------

    [Fact]
    public async Task Notification_job_is_enqueued_for_notifiable_transitions_only()
    {
        var outbox = new TaskNotificationOutbox(_fx.Store);
        var taskId = TaskId.New();
        var projectId = _fx.Project.Id;
        var now = _fx.Clock.GetUtcNow();

        await outbox.HandleAsync(new TaskStatusChanged(taskId, projectId, 1, null, AgentTaskStatus.Pending, null, now), CancellationToken.None);
        await outbox.HandleAsync(new TaskStatusChanged(taskId, projectId, 2, AgentTaskStatus.Pending, AgentTaskStatus.Planning, null, now), CancellationToken.None);
        await outbox.HandleAsync(new TaskStatusChanged(taskId, projectId, 3, AgentTaskStatus.Planning, AgentTaskStatus.WaitingForApproval, "auth", now), CancellationToken.None);
        await outbox.HandleAsync(new TaskStatusChanged(taskId, projectId, 3, AgentTaskStatus.Planning, AgentTaskStatus.WaitingForApproval, "auth", now), CancellationToken.None);

        _fx.Store.Jobs.Select(j => j.DeduplicationKey).ShouldBe([
            $"{JobTypes.SendNotification}:{taskId}:1",
            $"{JobTypes.SendNotification}:{taskId}:3",
        ]);
        _fx.Store.Jobs.ShouldAllBe(j => j.Type == JobTypes.SendNotification);
    }

    // ---------------- Workflow updates ----------------

    private UpdateRepositoryWorkflowHandler UpdateHandler() =>
        new(_fx.Access, _fx.Store, _fx.Store, _fx.Store, _fx.Store, MsOptions.Create(_fx.Limits), _fx.Clock);

    private static WorkflowDefinitionDto Dto(string plan = "ALWAYS", int fix = 2, string severity = "MEDIUM") => new(
        plan, "ON_SENSITIVE", 3, fix, severity, true,
        new PullRequestSettingsDto(true, ["devagent", "bug"], "[Bot]", ["octocat"]),
        ["TASK_ACCEPTED"], new ModelOverridesDto(null, null, null));

    [Fact]
    public async Task Owner_updates_workflow_creates_revision_and_audit()
    {
        _fx.ActAs(_fx.Owner);

        var result = await UpdateHandler().HandleAsync(new UpdateRepositoryWorkflowCommand(_fx.Repository.Id.Value, 0, Dto(), "siết duyệt"), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Version.ShouldBe(1);
        result.Value.Definition.PlanApproval.ShouldBe("ALWAYS");
        result.Value.Definition.NotifyOn.ShouldContain("APPROVAL_REQUIRED"); // mandatory, added automatically
        _fx.Store.Revisions.ShouldHaveSingleItem().Comment.ShouldBe("siết duyệt");
        _fx.Store.Audit.ShouldContain(a => a.Action == "repository.workflow.update");
    }

    [Fact]
    public async Task Requester_cannot_update_workflow()
    {
        var result = await UpdateHandler().HandleAsync(new UpdateRepositoryWorkflowCommand(_fx.Repository.Id.Value, 0, Dto(), null), CancellationToken.None);

        result.Error!.Type.ShouldBe(ErrorType.Forbidden);
        _fx.Repository.WorkflowVersion.ShouldBe(0);
    }

    [Fact]
    public async Task Stale_version_is_conflict()
    {
        _fx.ActAs(_fx.Owner);
        await UpdateHandler().HandleAsync(new UpdateRepositoryWorkflowCommand(_fx.Repository.Id.Value, 0, Dto(), null), CancellationToken.None);

        var result = await UpdateHandler().HandleAsync(new UpdateRepositoryWorkflowCommand(_fx.Repository.Id.Value, 0, Dto(fix: 1), null), CancellationToken.None);

        result.Error!.Type.ShouldBe(ErrorType.Conflict);
    }

    [Theory]
    [InlineData("NEVER", 2, "MEDIUM", "planApproval")]
    [InlineData("ALWAYS", 2, "CRITICAL", "reviewerBlockingSeverity")]
    [InlineData("ALWAYS", 9, "MEDIUM", "maxFixIterations")]
    public async Task Loosening_or_out_of_cap_values_are_rejected(string plan, int fix, string severity, string field)
    {
        _fx.ActAs(_fx.Owner);

        var result = await UpdateHandler().HandleAsync(
            new UpdateRepositoryWorkflowCommand(_fx.Repository.Id.Value, 0, Dto(plan, fix, severity), null), CancellationToken.None);

        result.Error!.Type.ShouldBe(ErrorType.Validation);
        result.Error.Details!.ShouldContainKey(field);
    }

    private sealed class FixedWorker : IWorkerIdentity
    {
        public string WorkerId => "w-test";
    }
}
