namespace DevAgent.Domain.Common;

/// <summary>A business fact that has already happened. <see cref="OccurredAt"/> is always UTC.</summary>
public interface IDomainEvent
{
    DateTimeOffset OccurredAt { get; }
}
