namespace SchoolMs.SharedKernel;

/// <summary>A domain event raised by an entity and dispatched after the change is saved.</summary>
public interface IDomainEvent;

/// <summary>Base class for entities with identity and domain events.</summary>
public abstract class Entity<TId>
    where TId : notnull
{
    private readonly List<IDomainEvent> _domainEvents = [];

    protected Entity(TId id)
    {
        Id = id;
    }

    /// <summary>Required by EF Core for materialisation.</summary>
    protected Entity()
    {
        Id = default!;
    }

    public TId Id { get; protected init; }

    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents;

    public void ClearDomainEvents() => _domainEvents.Clear();

    protected void Raise(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);
}
