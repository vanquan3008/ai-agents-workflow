namespace DevAgent.Domain.Policies;

/// <summary>Paths matching <see cref="Pattern"/> belong to <see cref="Area"/>.</summary>
public sealed record SensitivePathRule(string Pattern, SensitiveArea Area);
