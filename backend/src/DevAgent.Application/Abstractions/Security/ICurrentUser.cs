using DevAgent.Domain.Common;

namespace DevAgent.Application.Abstractions.Security;

/// <summary>
/// Người dùng của request hiện tại. Danh tính đến từ token đã xác thực, không bao giờ từ payload.
/// Trong Worker, giá trị là null (hành động mang danh nghĩa hệ thống/agent).
/// </summary>
public interface ICurrentUser
{
    UserId? UserId { get; }

    bool IsAuthenticated => UserId.HasValue;
}

/// <summary>Mã hóa đối xứng cho credential lưu trong DB (checklist 2A: lưu credential có mã hóa).</summary>
public interface ISecretProtector
{
    string Protect(string plaintext);

    string Unprotect(string protectedValue);
}
