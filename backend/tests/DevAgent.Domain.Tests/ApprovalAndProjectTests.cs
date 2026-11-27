using DevAgent.Domain.Approvals;
using DevAgent.Domain.Common;
using DevAgent.Domain.Policies;
using DevAgent.Domain.Projects;
using Shouldly;

namespace DevAgent.Domain.Tests;

public sealed class ApprovalTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 26, 8, 0, 0, TimeSpan.Zero);
    private static readonly PolicyDecision Decision = new(PolicyOutcome.RequiresApproval, SensitiveArea.Authentication, 1, ["auth"]);
    private const string Sha = "0123456789abcdef0123456789abcdef01234567";

    [Fact]
    public void Approval_is_bound_to_plan_version()
    {
        var approval = Approval.ForPlan(TaskId.New(), 2, Decision, Now, TimeSpan.FromHours(1));
        approval.Approve(UserId.New(), "ok", Now.AddMinutes(1));

        approval.IsValidFor(planVersion: 2, commitSha: null).ShouldBeTrue();
        approval.IsValidFor(planVersion: 3, commitSha: null).ShouldBeFalse();
    }

    [Fact]
    public void Diff_approval_is_bound_to_commit()
    {
        var approval = Approval.ForDiff(TaskId.New(), Sha, Decision, Now, TimeSpan.FromHours(1));
        approval.Approve(UserId.New(), null, Now);

        approval.IsValidFor(null, Sha.ToUpperInvariant()).ShouldBeTrue();
        approval.IsValidFor(null, "fedcba9876543210fedcba9876543210fedcba98").ShouldBeFalse();
    }

    [Fact]
    public void Expired_approval_cannot_be_decided()
    {
        var approval = Approval.ForPlan(TaskId.New(), 1, Decision, Now, TimeSpan.FromHours(1));

        Should.Throw<DomainException>(() => approval.Approve(UserId.New(), null, Now.AddHours(2))).Code.ShouldBe("approval.not_pending");
        approval.Status.ShouldBe(ApprovalStatus.Expired);
    }

    [Fact]
    public void Superseded_approval_is_no_longer_valid()
    {
        var approval = Approval.ForPlan(TaskId.New(), 1, Decision, Now, TimeSpan.FromHours(1));
        approval.Approve(UserId.New(), null, Now);
        approval.Supersede(Now);

        approval.IsValidFor(1, null).ShouldBeFalse();
    }

    [Fact]
    public void Decision_cannot_be_changed()
    {
        var approval = Approval.ForPlan(TaskId.New(), 1, Decision, Now, TimeSpan.FromHours(1));
        approval.Reject(UserId.New(), "không an toàn", Now);

        Should.Throw<DomainException>(() => approval.Approve(UserId.New(), null, Now));
    }
}

public sealed class ProjectTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 26, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Role_permissions_are_enforced()
    {
        var owner = UserId.New();
        var requester = UserId.New();
        var outsider = UserId.New();
        var project = Project.Create("Pilot", "pilot", owner, Now);
        project.AddOrUpdateMember(requester, ProjectRole.Requester, Now);

        project.HasPermission(owner, ProjectPermission.ApproveChanges).ShouldBeTrue();
        project.HasPermission(requester, ProjectPermission.CreateTasks).ShouldBeTrue();
        project.HasPermission(requester, ProjectPermission.ApproveChanges).ShouldBeFalse();
        project.HasPermission(outsider, ProjectPermission.ViewTasks).ShouldBeFalse();
    }

    [Fact]
    public void Last_owner_cannot_be_demoted()
    {
        var owner = UserId.New();
        var project = Project.Create("Pilot", "pilot", owner, Now);

        Should.Throw<DomainException>(() => project.AddOrUpdateMember(owner, ProjectRole.Viewer, Now)).Code.ShouldBe("project.last_owner");
    }

    [Theory]
    [InlineData("Has Space")]
    [InlineData("-leading")]
    [InlineData("trailing-")]
    public void Slug_must_be_kebab_case(string slug) =>
        Should.Throw<DomainException>(() => Project.Create("P", slug, UserId.New(), Now));
}
