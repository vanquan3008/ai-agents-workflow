namespace DevAgent.Application.Common;

/// <summary>Cập nhật đồng thời: bản ghi đã bị tiến trình khác sửa (optimistic concurrency).</summary>
public sealed class ConcurrencyConflictException : Exception
{
    public ConcurrencyConflictException()
    {
    }

    public ConcurrencyConflictException(string message)
        : base(message)
    {
    }

    public ConcurrencyConflictException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>Vi phạm khóa duy nhất (ví dụ cùng idempotency key). Hạ tầng dịch lỗi DB sang ngoại lệ này.</summary>
public sealed class DuplicateKeyException : Exception
{
    public DuplicateKeyException()
    {
    }

    public DuplicateKeyException(string message)
        : base(message)
    {
    }

    public DuplicateKeyException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public string? ConstraintName { get; init; }
}

/// <summary>Tích hợp bên ngoài (GitHub, Discord, AI, sandbox) chưa được cấu hình hoặc đang tắt.</summary>
public sealed class IntegrationNotConfiguredException : Exception
{
    public IntegrationNotConfiguredException()
    {
    }

    public IntegrationNotConfiguredException(string message)
        : base(message)
    {
    }

    public IntegrationNotConfiguredException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
