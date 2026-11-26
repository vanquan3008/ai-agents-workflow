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

/// <summary>Lỗi nghiệp vụ có mã ổn định. API ánh xạ <see cref="Type"/> sang mã HTTP và ProblemDetails.</summary>
public sealed record Error(string Code, string Message, ErrorType Type)
{
    public IReadOnlyDictionary<string, string[]>? Details { get; init; }

    public static Error Validation(string code, string message, IReadOnlyDictionary<string, string[]>? details = null) =>
        new(code, message, ErrorType.Validation) { Details = details };

    public static Error NotFound(string resource, object id) =>
        new($"{resource}.not_found", $"Không tìm thấy {resource} '{id}'.", ErrorType.NotFound);

    public static Error Forbidden(string message = "Bạn không có quyền thực hiện thao tác này.") =>
        new("auth.forbidden", message, ErrorType.Forbidden);

    public static Error Unauthorized() => new("auth.unauthenticated", "Cần đăng nhập.", ErrorType.Unauthorized);

    public static Error Conflict(string code, string message) => new(code, message, ErrorType.Conflict);

    public static Error Failure(string code, string message) => new(code, message, ErrorType.Failure);
}

public class Result
{
    protected Result(Error? error) => Error = error;

    public Error? Error { get; }

    public bool IsSuccess => Error is null;

    public bool IsFailure => !IsSuccess;

    public static Result Success() => new(null);

    public static Result Failure(Error error) => new(error ?? throw new ArgumentNullException(nameof(error)));

    public static implicit operator Result(Error error) => Failure(error);
}

#pragma warning disable CA1000 // Static factory trên kiểu generic là chủ đích cho Result<T>.
public sealed class Result<T> : Result
{
    private readonly T? _value;

    private Result(T value)
        : base(null) => _value = value;

    private Result(Error error)
        : base(error)
    {
    }

    public T Value => IsSuccess ? _value! : throw new InvalidOperationException("Không thể đọc Value của Result thất bại.");

    public static Result<T> Success(T value) => new(value);

    public static new Result<T> Failure(Error error) => new(error ?? throw new ArgumentNullException(nameof(error)));

    public static implicit operator Result<T>(T value) => Success(value);

    public static implicit operator Result<T>(Error error) => Failure(error);
}
#pragma warning restore CA1000
