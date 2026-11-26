using System.Text;

namespace DevAgent.Application.Common;

/// <summary>
/// Chuyển tên enum giữa dạng PascalCase (C#) và UPPER_SNAKE (API, khớp tài liệu: WAITING_FOR_APPROVAL).
/// </summary>
public static class ApiNames
{
    public static string ToApi<TEnum>(TEnum value)
        where TEnum : struct, Enum
    {
        // "GitHub" là một từ: GitHubIssue -> GITHUB_ISSUE thay vì GIT_HUB_ISSUE.
        var name = value.ToString().Replace("GitHub", "Github", StringComparison.Ordinal);
        var sb = new StringBuilder(name.Length + 8);
        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (i > 0 && char.IsUpper(c) && !char.IsUpper(name[i - 1]))
            {
                sb.Append('_');
            }

            sb.Append(char.ToUpperInvariant(c));
        }

        return sb.ToString();
    }

    /// <summary>Tách enum [Flags] thành danh sách tên API, bỏ giá trị 0.</summary>
    public static IReadOnlyList<string> FlagsToApi<TEnum>(TEnum value)
        where TEnum : struct, Enum
    {
        var raw = Convert.ToInt64(value, System.Globalization.CultureInfo.InvariantCulture);
        return Enum.GetValues<TEnum>()
            .Where(flag =>
            {
                var bits = Convert.ToInt64(flag, System.Globalization.CultureInfo.InvariantCulture);
                return bits != 0 && (raw & bits) == bits;
            })
            .Select(ToApi)
            .ToArray();
    }

    public static bool TryParse<TEnum>(string? value, out TEnum result)
        where TEnum : struct, Enum
    {
        result = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var compact = value.Replace("_", string.Empty, StringComparison.Ordinal);
        return Enum.TryParse(compact, ignoreCase: true, out result) && Enum.IsDefined(result);
    }
}
