namespace DevAgent.Domain.Common;

/// <summary>A violated business invariant. The API maps it to a 409/422 response with the stable <see cref="Code"/>.</summary>
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
