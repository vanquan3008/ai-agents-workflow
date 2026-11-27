using System.Security.Cryptography;
using System.Text;
using DevAgent.Application.Abstractions.Security;
using DevAgent.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace DevAgent.Infrastructure.Security;

/// <summary>
/// AES-256-GCM credential encryption. Format: base64(version | nonce | tag | ciphertext).
/// The key comes from <c>Security:EncryptionKey</c>; without it every operation fails.
/// </summary>
internal sealed class AesGcmSecretProtector(IOptions<SecurityOptions> options) : ISecretProtector
{
    private const byte Version = 1;
    private const int NonceSize = 12;
    private const int TagSize = 16;

    public string Protect(string plaintext)
    {
        ArgumentNullException.ThrowIfNull(plaintext);
        var key = Key();
        var data = Encoding.UTF8.GetBytes(plaintext);
        var output = new byte[1 + NonceSize + TagSize + data.Length];
        output[0] = Version;

        var nonce = output.AsSpan(1, NonceSize);
        RandomNumberGenerator.Fill(nonce);
        using var aes = new AesGcm(key, TagSize);
        aes.Encrypt(nonce, data, output.AsSpan(1 + NonceSize + TagSize), output.AsSpan(1 + NonceSize, TagSize));
        return Convert.ToBase64String(output);
    }

    public string Unprotect(string protectedValue)
    {
        ArgumentNullException.ThrowIfNull(protectedValue);
        var input = Convert.FromBase64String(protectedValue);
        if (input.Length < 1 + NonceSize + TagSize || input[0] != Version)
        {
            throw new CryptographicException("Encrypted payload is invalid or has an unsupported version.");
        }

        var key = Key();
        var plaintext = new byte[input.Length - 1 - NonceSize - TagSize];
        using var aes = new AesGcm(key, TagSize);
        aes.Decrypt(input.AsSpan(1, NonceSize), input.AsSpan(1 + NonceSize + TagSize), input.AsSpan(1 + NonceSize, TagSize), plaintext);
        return Encoding.UTF8.GetString(plaintext);
    }

    private byte[] Key() =>
        string.IsNullOrWhiteSpace(options.Value.EncryptionKey)
            ? throw new InvalidOperationException("Security:EncryptionKey is not configured.")
            : Convert.FromBase64String(options.Value.EncryptionKey);
}
