using DevAgent.Domain.Common;
using DevAgent.Domain.Policies;
using DevAgent.Domain.Tasks;
using DevAgent.Domain.Workflows;
using Shouldly;

namespace DevAgent.Domain.Tests;

public sealed class AgentTaskTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 26, 8, 0, 0, TimeSpan.Zero);
    private static readonly UserId Requester = UserId.New();
    private const string Sha = "0123456789abcdef0123456789abcdef01234567";

    private static AgentTask NewTask() => AgentTask.Create(
        ProjectId.New(),
        RepositoryId.New(),
        "Bộ lọc trả về sai trạng thái",
        "Lọc theo DONE vẫn trả task IN_PROGRESS.",
        new TaskSource(TaskChannel.Dashboard, null),
        new IdempotencyKey("req-1"),
        Requester,
        Now);

    [Fact]
    public void Create_starts_pending_and_records_first_transition()
    {
        var task = NewTask();

        task.Status.ShouldBe(AgentTaskStatus.Pending);
        task.Transitions.Count.ShouldBe(1);
        task.Transitions.Single().From.ShouldBeNull();
        task.DomainEvents.OfType<TaskCreated>().ShouldHaveSingleItem();
    }

    [Fact]
    public void Create_rejects_non_utc_timestamp()
    {
        Should.Throw<DomainException>(() => AgentTask.Create(
            ProjectId.New(), RepositoryId.New(), "t", "d", new TaskSource(TaskChannel.Api, null), new IdempotencyKey("k"), Requester,
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.FromHours(7))));
    }

    [Fact]
    public void Invalid_transition_is_rejected()
    {
        var task = NewTask();

        var ex = Should.Throw<DomainException>(() => task.StartTesting(Actor.System, Now));
        ex.Code.ShouldBe("task.invalid_transition");
    }

    [Fact]
    public void Complete_requires_publishing_state_and_stores_pull_request()
    {
        var task = NewTask();
        task.StartPlanning(Sha, "devagent/task-1", WorkflowDefinition.Default, 0, Actor.System, Now);
        task.StartCoding(Actor.System, "auto", Now);
        task.StartTesting(Actor.System, Now);
        task.StartReview(Actor.System, Now);

        var pr = new PullRequestLink(42, "https://github.com/acme/app/pull/42", "devagent/task-1", Sha);
        Should.Throw<DomainException>(() => task.Complete(pr, Actor.System, Now));

        task.StartPublishing(Actor.System, "ok", Now);
        task.Complete(pr, Actor.System, Now.AddMinutes(5));

        task.Status.ShouldBe(AgentTaskStatus.Completed);
        task.PullRequest.ShouldBe(pr);
        task.IsTerminal.ShouldBeTrue();
        task.Transitions.Select(t => t.Sequence).ShouldBe([1, 2, 3, 4, 5, 6, 7]);
        task.WorkflowVersion.ShouldBe(0);
    }

    [Fact]
    public void Cancel_is_idempotent_and_terminal()
    {
        var task = NewTask();
        task.Cancel(Requester, null, Now);
        task.Cancel(Requester, null, Now);

        task.Status.ShouldBe(AgentTaskStatus.Cancelled);
        task.Transitions.Count.ShouldBe(2);
        Should.Throw<DomainException>(() => task.StartPlanning(Sha, "b", WorkflowDefinition.Default, 0, Actor.System, Now));
    }

    [Fact]
    public void Attach_plan_increments_version_and_keeps_policy_decision()
    {
        var task = NewTask();
        task.StartPlanning(Sha, "devagent/task-1", WorkflowDefinition.Default, 0, Actor.System, Now);
        var decision = new PolicyDecision(PolicyOutcome.RequiresApproval, SensitiveArea.Migration, 3, ["migration"]);

        var v1 = task.AttachPlan("s", ["a"], ["c"], ["r"], ["src/x.cs"], decision, Now);
        var v2 = task.AttachPlan("s2", ["a"], ["c"], ["r"], ["src/x.cs"], decision, Now);

        v1.Version.ShouldBe(1);
        v2.Version.ShouldBe(2);
        task.CurrentPlan!.Version.ShouldBe(2);
        v2.SensitiveAreas.ShouldBe(SensitiveArea.Migration);
        v2.PolicyVersion.ShouldBe(3);
    }

    [Fact]
    public void Active_duration_excludes_waiting_for_approval()
    {
        var task = NewTask();
        task.StartPlanning(Sha, "b", WorkflowDefinition.Default, 0, Actor.System, Now.AddMinutes(1));
        task.WaitForApproval("cần duyệt", Actor.System, Now.AddMinutes(11));
        task.StartCoding(Actor.System, "đã duyệt", Now.AddHours(5));

        // Pending 1' + Planning 10' + InProgress 2' = 13'; 5 giờ chờ duyệt không tính.
        task.ActiveDuration(Now.AddHours(5).AddMinutes(2)).ShouldBe(TimeSpan.FromMinutes(13));
    }

    [Fact]
    public void Pending_task_can_fail_or_be_blocked()
    {
        var task = NewTask();
        task.Fail("GitHub không truy cập được", Actor.System, Now);
        task.Status.ShouldBe(AgentTaskStatus.Failed);
    }

    [Fact]
    public void Planning_round_start_resets_after_retry()
    {
        var task = NewTask();
        task.StartPlanning(Sha, "b", WorkflowDefinition.Default, 0, Actor.System, Now);
        task.Fail("x", Actor.System, Now.AddMinutes(1));
        task.Retry(Requester, 2, Now.AddMinutes(2));
        task.StartPlanning(Sha, "b", WorkflowDefinition.Default, 3, Actor.System, Now.AddMinutes(3));

        task.CurrentPlanningStartedAt.ShouldBe(Now.AddMinutes(3));
        task.WorkflowVersion.ShouldBe(3);
    }

    [Fact]
    public void Retry_is_limited()
    {
        var task = NewTask();
        task.StartPlanning(Sha, "b", WorkflowDefinition.Default, 0, Actor.System, Now);
        task.Fail("boom", Actor.System, Now);

        task.Retry(Requester, maxRetries: 1, Now);
        task.Status.ShouldBe(AgentTaskStatus.Pending);

        task.StartPlanning(Sha, "b", WorkflowDefinition.Default, 0, Actor.System, Now);
        task.Fail("boom", Actor.System, Now);
        Should.Throw<DomainException>(() => task.Retry(Requester, maxRetries: 1, Now)).Code.ShouldBe("task.retry_exhausted");
    }

    [Theory]
    [InlineData("")]
    [InlineData("has space")]
    [InlineData("emoji-😀")]
    public void Idempotency_key_rejects_invalid_values(string value) =>
        Should.Throw<DomainException>(() => new IdempotencyKey(value));

    [Theory]
    [InlineData("http://github.com/a/b/pull/1")]
    [InlineData("not-a-url")]
    public void Pull_request_link_requires_https(string url) =>
        Should.Throw<DomainException>(() => new PullRequestLink(1, url, "b", Sha));
}
