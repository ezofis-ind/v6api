namespace SaaSApp.SharedKernel.Domain;

/// <summary>
/// Base entity with a typed key.
/// </summary>
public abstract class Entity<TKey> : Entity
    where TKey : notnull
{
    public TKey Id { get; protected set; } = default!;
}
