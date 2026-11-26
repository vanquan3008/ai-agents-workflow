using DevAgent.Domain.Workflows;
using DevAgent.Infrastructure.Persistence.Converters;
using Shouldly;

namespace DevAgent.Infrastructure.Tests;

public sealed class WorkflowJsonTests
{
    [Fact]
    public void Empty_json_reads_as_system_default()
    {
        // Migration đặt "{}" cho các repo đã có; phải tương đương quy trình mặc định.
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
