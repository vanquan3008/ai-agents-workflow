using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using DevAgent.Application.Abstractions.Security;

namespace DevAgent.Infrastructure.Security;

/// <summary>
/// Băm mật khẩu bằng PBKDF2-HMAC-SHA512 có muối ngẫu nhiên 16 byte.
/// Định dạng tự mô tả: <c>pbkdf2-sha512${iterations}${salt}${hash}</c> (base64), nên có thể tăng số vòng lặp
/// mà vẫn kiểm tra được chuỗi băm cũ (trả <see cref="PasswordVerification.SuccessRehashNeeded"/> để băm lại).
/// So sánh bằng thời gian hằng.
/// </summary>
internal sealed class Pbkdf2PasswordHasher : IPasswordHasher
{
    /// <summary>Theo khuyến nghị OWASP 2023+ cho PBKDF2-HMAC-SHA512.</summary>
    public const int Iterations = 210_000;

    private const string Algorithm = "pbkdf2-sha512";
    private const int SaltSize = 16;
    private const int HashSize = 32;

    public string Hash(string password)
    {
        ArgumentNullException.ThrowIfNull(password);
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), salt, Iterations, HashAlgorithmName.SHA512, HashSize);
        return string.Join('$', Algorithm, Iterations.ToString(CultureInfo.InvariantCulture), Convert.ToBase64String(salt), Convert.ToBase64String(hash));
    }

    public PasswordVerification Verify(string passwordHash, string password)
    {
        ArgumentNullException.ThrowIfNull(password);
        var parts = (passwordHash ?? string.Empty).Split('$');
        if (parts.Length != 4 || parts[0] != Algorithm || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var iterations) || iterations <= 0)
        {
            return PasswordVerification.Failed;
        }

        byte[] salt;
        byte[] expected;
        try
        {
            salt = Convert.FromBase64String(parts[2]);
            expected = Convert.FromBase64String(parts[3]);
        }
        catch (FormatException)
        {
            return PasswordVerification.Failed;
        }

        var actual = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), salt, iterations, HashAlgorithmName.SHA512, expected.Length);
        if (!CryptographicOperations.FixedTimeEquals(actual, expected))
        {
            return PasswordVerification.Failed;
        }

        return iterations < Iterations ? PasswordVerification.SuccessRehashNeeded : PasswordVerification.Success;
    }
}
