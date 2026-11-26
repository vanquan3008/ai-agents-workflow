using System.Diagnostics;
using System.Text.RegularExpressions;
using Serilog.Core;
using Serilog.Events;

namespace DevAgent.Infrastructure.Observability;

/// <summary>
/// Che dữ liệu nhạy cảm trước khi ghi log (checklist 1.5, kịch bản nghiệm thu "log không chứa token"):
/// - thuộc tính có tên giống secret (token, password, apiKey, authorization...) bị thay bằng "***";
/// - giá trị chuỗi khớp định dạng token phổ biến (GitHub, Anthropic, Bearer, Bot, JWT) bị che.
/// </summary>
internal sealed partial class SensitiveDataMaskingEnricher : ILogEventEnricher
{
    private const string Mask = "***";

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        ArgumentNullException.ThrowIfNull(logEvent);
        foreach (var (name, value) in logEvent.Properties.ToArray())
        {
            if (SensitiveName().IsMatch(name))
            {
                logEvent.AddOrUpdateProperty(new LogEventProperty(name, new ScalarValue(Mask)));
                continue;
            }

            var masked = MaskValue(value);
            if (!ReferenceEquals(masked, value))
            {
                logEvent.AddOrUpdateProperty(new LogEventProperty(name, masked));
            }
        }
    }

    internal static string MaskString(string input) => SensitiveValue().Replace(input, m => m.Value[..Math.Min(4, m.Value.Length)] + Mask);

    private static LogEventPropertyValue MaskValue(LogEventPropertyValue value) => value switch
    {
        ScalarValue { Value: string s } when SensitiveValue().IsMatch(s) => new ScalarValue(MaskString(s)),
        StructureValue structure => MaskStructure(structure),
        _ => value,
    };

    private static StructureValue MaskStructure(StructureValue structure)
    {
        var changed = false;
        var properties = structure.Properties.Select(p =>
        {
            LogEventPropertyValue next = SensitiveName().IsMatch(p.Name) ? new ScalarValue(Mask) : MaskValue(p.Value);
            changed |= !ReferenceEquals(next, p.Value);
            return new LogEventProperty(p.Name, next);
        }).ToArray();
        return changed ? new StructureValue(properties, structure.TypeTag) : structure;
    }

    [GeneratedRegex("(token|secret|password|passwd|api[_-]?key|authorization|private[_-]?key|connectionstring|cookie)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SensitiveName();

    [GeneratedRegex(@"(gh[pousr]_[A-Za-z0-9]{20,}|github_pat_[A-Za-z0-9_]{20,}|sk-ant-[A-Za-z0-9_\-]{10,}|(?i:bearer|bot)\s+[A-Za-z0-9._\-]{16,}|eyJ[A-Za-z0-9_\-]{10,}\.[A-Za-z0-9_\-]{10,}\.[A-Za-z0-9_\-]{10,})", RegexOptions.CultureInvariant)]
    private static partial Regex SensitiveValue();
}

/// <summary>Gắn TraceId/SpanId vào mọi dòng log để nối log với trace OpenTelemetry.</summary>
internal sealed class TraceContextEnricher : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        ArgumentNullException.ThrowIfNull(logEvent);
        ArgumentNullException.ThrowIfNull(propertyFactory);
        if (Activity.Current is { } activity)
        {
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("TraceId", activity.TraceId.ToHexString()));
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("SpanId", activity.SpanId.ToHexString()));
        }
    }
}
