namespace Ordering.Domain.Abstractions;

/// <summary>
/// An object defined by its identity rather than by its attributes.
/// </summary>
public abstract class Entity<TId>
    where TId : notnull
{
    protected Entity(TId id)
    {
        Id = id;
    }

    // Required by EF Core materialization.
    protected Entity()
    {
        Id = default!;
    }

    public TId Id { get; private set; }
}
