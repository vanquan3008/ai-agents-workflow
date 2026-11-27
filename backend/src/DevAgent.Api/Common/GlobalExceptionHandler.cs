using System.Diagnostics;
using DevAgent.Application.Common;
using DevAgent.Domain.Common;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace DevAgent.Api.Common;

/// <summary>
/// Converts unhandled exceptions to ProblemDetails: domain invariant violations → 422, concurrency conflicts → 409,
/// anything else → 500 without leaking stack traces; the traceId links the response to logs.
/// </summary>
internal sealed partial class GlobalExceptionHandler(IProblemDetailsService problemDetails, ILogger<GlobalExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (status, title, code) = exception switch
        {
            DomainException domain => (StatusCodes.Status422UnprocessableEntity, "Vi phạm quy tắc nghiệp vụ", domain.Code),
            ConcurrencyConflictException => (StatusCodes.Status409Conflict, "Dữ liệu đã thay đổi", "concurrency.conflict"),
            DuplicateKeyException => (StatusCodes.Status409Conflict, "Dữ liệu trùng", "duplicate"),
            BadHttpRequestException => (StatusCodes.Status400BadRequest, "Yêu cầu không hợp lệ", "request.invalid"),
            _ => (StatusCodes.Status500InternalServerError, "Lỗi hệ thống", "internal_error"),
        };

        if (status >= 500)
        {
            LogUnhandled(logger, exception);
        }

        httpContext.Response.StatusCode = status;
        var details = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = status >= 500 ? "Đã xảy ra lỗi không mong muốn." : exception.Message,
            Instance = httpContext.Request.Path,
        };
        details.Extensions["code"] = code;
        details.Extensions["traceId"] = Activity.Current?.TraceId.ToHexString() ?? httpContext.TraceIdentifier;

        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = details,
            Exception = exception,
        });
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Unhandled exception while processing the request")]
    private static partial void LogUnhandled(ILogger logger, Exception exception);
}
