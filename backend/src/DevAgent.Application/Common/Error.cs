namespace DevAgent.Application.Common;

public enum ErrorType
{
    Validation = 1,
    NotFound = 2,
    Forbidden = 3,
    Unauthorized = 4,
    Conflict = 5,
    Failure = 6,
}

/// <summary>Business error with a stable code. The API maps <see cref="Type"/> to an HTTP status and ProblemDetails.</summary>
public sealed record Error(string Code, string Message, ErrorType Type)
{
    public IReadOnlyDictionary<string, string[]>? Details { get; init; }

    public static Error Validation(string code, string message, IReadOnlyDictionary<string, string[]>? details = null) =>
        new(code, message, ErrorType.Validation) { Details = details };

    /// <summary>Validation error attached to a single input field.</summary>
    public static Error Field(string code, string field, string message) =>
        Validation(code, message, new Dictionary<string, string[]> { [field] = [message] });

    public static Error NotFound(string resource, object id) =>
        new($"{resource}.not_found", $"Không tìm thấy {resource} '{id}'.", ErrorType.NotFound);

    public static Error Forbidden(string message = "Bạn không có quyền thực hiện thao tác này.") =>
        new("auth.forbidden", message, ErrorType.Forbidden);

    public static Error Unauthorized() => new("auth.unauthenticated", "Cần đăng nhập.", ErrorType.Unauthorized);

    public static Error Conflict(string code, string message) => new(code, message, ErrorType.Conflict);

    public static Error Failure(string code, string message) => new(code, message, ErrorType.Failure);
}
