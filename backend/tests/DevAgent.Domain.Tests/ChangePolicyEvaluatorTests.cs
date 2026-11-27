using DevAgent.Domain.Policies;
using Shouldly;

namespace DevAgent.Domain.Tests;

public sealed class ChangePolicyEvaluatorTests
{
    private static readonly RepositoryPolicy Policy = RepositoryPolicy.CreateDefault("dotnet build", "dotnet test");

    private static PolicyDecision Evaluate(params (string Path, ChangeKind Kind)[] changes) =>
        ChangePolicyEvaluator.Evaluate(Policy, changes.Select(c => new ProposedChange(c.Path, c.Kind)).ToArray());

    [Fact]
    public void Changes_inside_auto_scope_are_auto_approved()
    {
        var decision = Evaluate(("src/Tasks/TaskFilter.cs", ChangeKind.Modify), ("tests/Tasks/TaskFilterTests.cs", ChangeKind.Create));

        decision.Outcome.ShouldBe(PolicyOutcome.AutoApproved);
        decision.Areas.ShouldBe(SensitiveArea.None);
        decision.PolicyVersion.ShouldBe(1);
    }

    [Theory]
    [InlineData("src/Auth/LoginService.cs", SensitiveArea.Authentication)]
    [InlineData("src/Security/PermissionService.cs", SensitiveArea.Authorization)]
    [InlineData("src/Data/Migrations/20260101_Init.cs", SensitiveArea.Migration)]
    [InlineData(".github/workflows/ci.yml", SensitiveArea.CiCd)]
    [InlineData("src/Api/appsettings.Production.json", SensitiveArea.Credentials)]
    public void Sensitive_paths_require_approval(string path, SensitiveArea expected)
    {
        var decision = Evaluate((path, ChangeKind.Modify));

        decision.Outcome.ShouldBe(PolicyOutcome.RequiresApproval);
        decision.Areas.HasFlag(expected).ShouldBeTrue();
    }

    [Fact]
    public void Delete_is_destructive_and_requires_approval()
    {
        var decision = Evaluate(("src/Tasks/Old.cs", ChangeKind.Delete));

        decision.Outcome.ShouldBe(PolicyOutcome.RequiresApproval);
        decision.Areas.HasFlag(SensitiveArea.Destructive).ShouldBeTrue();
    }

    [Fact]
    public void Path_outside_auto_scope_requires_approval()
    {
        Evaluate(("docs/readme.md", ChangeKind.Modify)).Outcome.ShouldBe(PolicyOutcome.RequiresApproval);
    }

    [Theory]
    [InlineData("../etc/passwd")]
    [InlineData("/etc/passwd")]
    [InlineData("C:/Windows/system32")]
    [InlineData("src/../../escape.cs")]
    public void Path_escaping_workspace_needs_review(string path)
    {
        Evaluate((path, ChangeKind.Modify)).Outcome.ShouldBe(PolicyOutcome.NeedsReview);
    }

    [Fact]
    public void Empty_change_set_needs_review()
    {
        ChangePolicyEvaluator.Evaluate(Policy, []).Outcome.ShouldBe(PolicyOutcome.NeedsReview);
    }

    [Fact]
    public void Too_many_files_requires_approval()
    {
        var changes = Enumerable.Range(0, Policy.MaxChangedFiles + 1)
            .Select(i => new ProposedChange($"src/File{i}.cs", ChangeKind.Modify))
            .ToArray();

        ChangePolicyEvaluator.Evaluate(Policy, changes).Outcome.ShouldBe(PolicyOutcome.RequiresApproval);
    }
}

public sealed class PathPatternTests
{
    [Theory]
    [InlineData("src/**", "src/a/b/c.cs", true)]
    [InlineData("src/**", "srcx/a.cs", false)]
    [InlineData("**/Migrations/**", "Migrations/x.cs", true)]
    [InlineData("**/Migrations/**", "a/b/Migrations/x.cs", true)]
    [InlineData("*.json", "a/b.json", false)]
    [InlineData("**/*.json", "a/b.json", true)]
    [InlineData("src/?.cs", "src/a.cs", true)]
    [InlineData("src/?.cs", "src/ab.cs", false)]
    public void Glob_matching(string pattern, string path, bool expected) =>
        new PathPattern(pattern).Matches(path).ShouldBe(expected);

    [Theory]
    [InlineData("a/./b/../c.cs", "a/c.cs")]
    [InlineData("a\\b.cs", "a/b.cs")]
    public void Normalize_resolves_relative_segments(string input, string expected) =>
        PathPattern.Normalize(input).ShouldBe(expected);
}
