using System.Text.RegularExpressions;

namespace DevAgent.Domain.Workflows;

internal static partial class WorkflowPatterns
{
    [GeneratedRegex("^[A-Za-z0-9](?:[A-Za-z0-9]|-(?=[A-Za-z0-9])){0,38}$", RegexOptions.CultureInvariant)]
    public static partial Regex GitHubLogin();

    [GeneratedRegex("^[a-z0-9][a-z0-9.\\-]{0,99}$", RegexOptions.CultureInvariant)]
    public static partial Regex ModelId();
}
