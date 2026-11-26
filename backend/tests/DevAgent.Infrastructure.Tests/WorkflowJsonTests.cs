using DevAgent.Domain.Workflows;
using DevAgent.Infrastructure.Persistence.Converters;
using Shouldly;

namespace DevAgent.Infrastructure.Tests;

public sealed class WorkflowJsonTests
{
    [Fact]
    public void Empty_json_reads_as_system_default()
    {
        // The migration stored "{}" for existing repositories; it must equal the default workflow.
        WorkflowJson.Deserialize("{}").ShouldBe(WorkflowDefinition.Default);
    }

    [Fact]
    public void Round_trip_preserves_content()
    {
        var workflow = new WorkflowDefinition(
            ApprovalRequirement.Always, ApprovalRequirement.Always, 2, 1, FindingSeverity.Low, false,
            new PullRequestSettings(true, ["bug"], "[Bot]", ["octocat"]),
            [NotificationEvent.TaskAccepted],
            new StepModelOverrides("claude-sonnet-5-5", null, "claude-opus-5-5"));

        var json = WorkflowJson.Serialize(workflow);

        json.ShouldContain("\"Always\"");
        WorkflowJson.Deserialize(json).ShouldBe(workflow);
    }
}
