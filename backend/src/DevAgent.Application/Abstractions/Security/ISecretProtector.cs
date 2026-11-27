namespace DevAgent.Application.Abstractions.Security;

/// <summary>Symmetric encryption for credentials stored in the database.</summary>
public interface ISecretProtector
{
    string Protect(string plaintext);

    string Unprotect(string protectedValue);
}
