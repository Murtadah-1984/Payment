namespace Payment.Domain.ValueObjects;

/// <summary>
/// Value object representing a security incident identifier.
/// Follows Value Object pattern - immutable and validated.
/// </summary>
public sealed record SecurityIncidentId(Guid Value)
{
    public static SecurityIncidentId NewId() => new(Guid.NewGuid());
    public static SecurityIncidentId FromGuid(Guid value) => new(value);
}

