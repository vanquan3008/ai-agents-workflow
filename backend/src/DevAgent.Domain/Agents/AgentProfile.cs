using DevAgent.Domain.Common;
using DevAgent.Domain.Executions;

namespace DevAgent.Domain.Agents;

/// <summary>
/// Cấu hình agent (AgentProfile): vai trò, model, công cụ được phép, trần đầu ra.
/// Khác với AgentInstance (tiến trình đang chạy) và AgentExecution (một lần chạy).
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

    /// <summary>Khóa model logic; adapter nhà cung cấp AI ánh xạ sang model id thật qua cấu hình.</summary>
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
