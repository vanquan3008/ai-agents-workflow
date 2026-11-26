namespace DevAgent.Domain.Common;

/// <summary>Vi phạm bất biến nghiệp vụ. Tầng API chuyển thành lỗi 409/422 có mã ổn định.</summary>
public class DomainException : Exception
{
    public DomainException(string code, string message)
        : base(message) => Code = code;

    public DomainException()
    {
        Code = "domain.error";
    }

    public DomainException(string message)
        : base(message) => Code = "domain.error";

    public DomainException(string message, Exception innerException)
        : base(message, innerException) => Code = "domain.error";

    public string Code { get; }
}
