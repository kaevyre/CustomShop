namespace Reviews.Domain.Common;

/// <summary>
/// Value Object: незмінний, без ідентичності; два об'єкти рівні, якщо рівні всі їхні компоненти.
/// </summary>
public abstract class ValueObject
{
    protected abstract IEnumerable<object?> GetEqualityComponents();

    public override bool Equals(object? obj)
        => obj is ValueObject other && GetType() == other.GetType()
           && GetEqualityComponents().SequenceEqual(other.GetEqualityComponents());

    public override int GetHashCode()
        => GetEqualityComponents().Aggregate(0, (hash, part) => HashCode.Combine(hash, part));

    public static bool operator ==(ValueObject? left, ValueObject? right) => Equals(left, right);
    public static bool operator !=(ValueObject? left, ValueObject? right) => !Equals(left, right);
}
