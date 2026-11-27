using System.Text.RegularExpressions;
using DevAgent.Domain.Common;

namespace DevAgent.Domain.Tasks;

public static partial class CommitSha
{
    public static string Validate(string? sha)
    {
        var value = Guard.NotEmpty(sha, "commit_sha", 64).ToLowerInvariant();
        if (!Pattern().IsMatch(value))
        {
            throw new DomainException("commit_sha.invalid", "Commit SHA không hợp lệ.");
        }

        return value;
    }

    [GeneratedRegex("^[0-9a-f]{7,64}$", RegexOptions.CultureInvariant)]
    private static partial Regex Pattern();
}
