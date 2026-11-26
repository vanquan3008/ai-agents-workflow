using System.Text.RegularExpressions;
using DevAgent.Domain.Common;

namespace DevAgent.Domain.Tasks;

/// <summary>
/// Deduplication key: the same (project, key) pair yields exactly one task.
/// Chat and webhook channels use the event id; the dashboard and API use the <c>Idempotency-Key</c> header.
/// </summary>
public sealed partial record IdempotencyKey
{
    public const int MaxLength = 200;

    public IdempotencyKey(string value)
    {
        var trimmed = Guard.NotEmpty(value, "idempotency_key", MaxLength);
        if (!AllowedChars().IsMatch(trimmed))
        {
            throw new DomainException("idempotency_key.invalid", "Idempotency key chỉ gồm chữ, số và các ký tự . _ : -");
        }

        Value = trimmed;
    }

    public string Value { get; }

    public override string ToString() => Value;

    [GeneratedRegex("^[A-Za-z0-9._:-]+$", RegexOptions.CultureInvariant)]
    private static partial Regex AllowedChars();
}
