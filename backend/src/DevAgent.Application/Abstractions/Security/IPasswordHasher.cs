namespace DevAgent.Application.Abstractions.Security;

public enum PasswordVerification
{
    Failed = 0,
    Success = 1,

    /// <summary>Correct password stored with outdated parameters; it should be re-hashed.</summary>
    SuccessRehashNeeded = 2,
}

/// <summary>Salted slow hashing (PBKDF2) with constant-time comparison.</summary>
public interface IPasswordHasher
{
    string Hash(string password);

    PasswordVerification Verify(string passwordHash, string password);
}
