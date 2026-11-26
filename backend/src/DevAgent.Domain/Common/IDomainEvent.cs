namespace DevAgent.Domain.Common;

/// <summary>Sự kiện nghiệp vụ đã xảy ra. Thời điểm luôn là UTC.</summary>
public interface IDomainEvent
{
    DateTimeOffset OccurredAt { get; }
}

public interface IHasDomainEvents
{
    IReadOnlyCollection<IDomainEvent> DomainEvents { get; }

    void ClearDomainEvents();
}
