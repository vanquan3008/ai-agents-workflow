using DevAgent.Domain.Common;
using DevAgent.Domain.Executions;

namespace DevAgent.Domain.Agents;

/// <summary>
/// Agent configuration: role, model, allowed tools and output cap.
/// Distinct from <see cref="AgentInstance"/> (a running process) and AgentExecution (a single run).
/// </summary>
public sealed class AgentProfile : AggregateRoot<AgentProfileId>
{
    private AgentProfile()
    {
        Name = string.Empty;
        Model = string.Empty;
        AllowedTools = [];
    }

    private AgentProfile(AgentProfileId id, string name, AgentStep role, string model, IReadOnlyList<string> allowedTools, int maxOutputTokens)
        : base(id)
    {
        Name = name;
        Role = role;
        Model = model;
        AllowedTools = [.. allowedTools];
        MaxOutputTokens = maxOutputTokens;
    }

    public string Name { get; private set; }

    public AgentStep Role { get; private set; }

    /// <summary>Logical model key; the AI provider adapter maps it to a concrete model id through configuration.</summary>
    public string Model { get; private set; }

    public List<string> AllowedTools { get; private set; }

    public int MaxOutputTokens { get; private set; }

    public bool IsEnabled { get; private set; } = true;

    public static AgentProfile Create(string name, AgentStep role, string model, IReadOnlyList<string> allowedTools, int maxOutputTokens)
    {
        ArgumentNullException.ThrowIfNull(allowedTools);
        if (maxOutputTokens is < 256 or > 128_000)
        {
            throw new DomainException("agent_profile.max_output_invalid", "MaxOutputTokens phải trong khoảng 256..128000.");
        }

        return new AgentProfile(
            AgentProfileId.New(),
            Guard.NotEmpty(name, "name", 100),
            role,
            Guard.NotEmpty(model, "model", 100),
            allowedTools.Select(t => Guard.NotEmpty(t, "tool", 100)).Distinct(StringComparer.Ordinal).ToArray(),
            maxOutputTokens);
    }

    public bool CanUseTool(string toolName) => IsEnabled && AllowedTools.Contains(toolName, StringComparer.Ordinal);

    public void Disable() => IsEnabled = false;
}
