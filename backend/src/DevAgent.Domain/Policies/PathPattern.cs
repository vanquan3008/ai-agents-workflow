using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using DevAgent.Domain.Common;

namespace DevAgent.Domain.Policies;

/// <summary>
/// Mẫu đường dẫn dạng glob, so khớp trên đường dẫn tương đối dùng dấu '/'.
/// Hỗ trợ <c>**</c> (nhiều cấp thư mục), <c>*</c> (trong một cấp) và <c>?</c> (một ký tự).
/// </summary>
public sealed class PathPattern
{
    private readonly Regex _regex;

    public PathPattern(string pattern)
    {
        Pattern = Guard.NotEmpty(pattern, "path_pattern", 500).Replace('\\', '/');
        _regex = new Regex(ToRegex(Pattern), RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(100));
    }

    public string Pattern { get; }

    public bool Matches(string relativePath)
    {
        var normalized = Normalize(relativePath);
        return normalized is not null && _regex.IsMatch(normalized);
    }

    /// <summary>Chuẩn hóa đường dẫn; trả null nếu đường dẫn thoát ra ngoài workspace hoặc là tuyệt đối.</summary>
    public static string? Normalize(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return null;
        }

        var path = relativePath.Replace('\\', '/').Trim();
        if (path.StartsWith('/') || path.Contains(':', StringComparison.Ordinal))
        {
            return null;
        }

        var segments = new List<string>();
        foreach (var segment in path.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (segment == ".")
            {
                continue;
            }

            if (segment == "..")
            {
                if (segments.Count == 0)
                {
                    return null;
                }

                segments.RemoveAt(segments.Count - 1);
                continue;
            }

            segments.Add(segment);
        }

        return segments.Count == 0 ? null : string.Join('/', segments);
    }

    public override string ToString() => Pattern;

    private static string ToRegex(string glob)
    {
        var sb = new StringBuilder("^");
        for (var i = 0; i < glob.Length; i++)
        {
            var c = glob[i];
            if (c == '*' && i + 1 < glob.Length && glob[i + 1] == '*')
            {
                // "**/" khớp không hoặc nhiều thư mục; "**" ở cuối khớp mọi thứ.
                if (i + 2 < glob.Length && glob[i + 2] == '/')
                {
                    sb.Append("(?:.*/)?");
                    i += 2;
                }
                else
                {
                    sb.Append(".*");
                    i++;
                }
            }
            else if (c == '*')
            {
                sb.Append("[^/]*");
            }
            else if (c == '?')
            {
                sb.Append("[^/]");
            }
            else
            {
                sb.Append(Regex.Escape(c.ToString(CultureInfo.InvariantCulture)));
            }
        }

        return sb.Append('$').ToString();
    }
}
