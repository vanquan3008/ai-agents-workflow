namespace DevAgent.Domain.Common;

internal static class Guard
{
    public static string NotEmpty(string? value, string field, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainException($"{field}.required", $"{field} là bắt buộc.");
        }

        var trimmed = value.Trim();
        if (trimmed.Length > maxLength)
        {
            throw new DomainException($"{field}.too_long", $"{field} vượt quá {maxLength} ký tự.");
        }

        return trimmed;
    }

    public static string? Optional(string? value, string field, int maxLength) =>
        string.IsNullOrWhiteSpace(value) ? null : NotEmpty(value, field, maxLength);

    public static DateTimeOffset Utc(DateTimeOffset value, string field)
    {
        if (value.Offset != TimeSpan.Zero)
        {
            throw new DomainException($"{field}.not_utc", $"{field} phải ở múi giờ UTC.");
        }

        return value;
    }

    public static T NotDefault<T>(T value, string field)
        where T : struct
    {
        if (EqualityComparer<T>.Default.Equals(value, default))
        {
            throw new DomainException($"{field}.required", $"{field} là bắt buộc.");
        }

        return value;
    }
}
