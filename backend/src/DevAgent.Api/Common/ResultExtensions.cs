using DevAgent.Application.Common;

namespace DevAgent.Api.Common;

/// <summary>Ánh xạ <see cref="Result{T}"/> sang HTTP: thành công trả dữ liệu, lỗi trả ProblemDetails (RFC 9457) có mã ổn định.</summary>
internal static class ResultExtensions
{
    public static IResult ToHttp<T>(this Result<T> result, Func<T, IResult>? onSuccess = null)
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.IsSuccess ? (onSuccess ?? (v => Results.Ok(v)))(result.Value) : result.Error!.ToProblem();
    }

    public static IResult ToProblem(this Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        var extensions = new Dictionary<string, object?> { ["code"] = error.Code };

        if (error.Type == ErrorType.Validation && error.Details is { Count: > 0 })
        {
            return Results.ValidationProblem(
                error.Details.ToDictionary(kv => kv.Key, kv => kv.Value),
                detail: error.Message,
                title: "Dữ liệu không hợp lệ",
                extensions: extensions);
        }

        var (status, title) = error.Type switch
        {
            ErrorType.Validation => (StatusCodes.Status400BadRequest, "Dữ liệu không hợp lệ"),
            ErrorType.NotFound => (StatusCodes.Status404NotFound, "Không tìm thấy"),
            ErrorType.Forbidden => (StatusCodes.Status403Forbidden, "Không có quyền"),
            ErrorType.Unauthorized => (StatusCodes.Status401Unauthorized, "Chưa xác thực"),
            ErrorType.Conflict => (StatusCodes.Status409Conflict, "Xung đột trạng thái"),
            _ => (StatusCodes.Status500InternalServerError, "Lỗi hệ thống"),
        };

        return Results.Problem(detail: error.Message, statusCode: status, title: title, extensions: extensions);
    }
}
