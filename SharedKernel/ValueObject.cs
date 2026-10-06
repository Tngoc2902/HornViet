namespace Website.Domain.SharedKernel;

/// <summary>Đối tượng giá trị (Value Object) — so sánh bằng giá trị của các thành phần, không bằng định danh.</summary>
public abstract class ValueObject : IEquatable<ValueObject>
{
    public abstract IEnumerable<object> GetEqualityComponents();

    public override bool Equals(object? obj)
    {
        if (obj is null || obj.GetType() != GetType())
        {
            return false;
        }

        var valueObject = (ValueObject)obj;
        return GetEqualityComponents()
                .SequenceEqual(valueObject.GetEqualityComponents());
    }

    public static bool operator ==(ValueObject left, ValueObject right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(ValueObject left, ValueObject right)
    {
        return !Equals(left, right);
    }

    public override int GetHashCode()
    {
        return GetEqualityComponents()
                .Select(x => x?.GetHashCode() ?? 0)
                .Aggregate((x, y) => x ^ y);
    }

    public bool Equals(ValueObject? other)
    {
        return Equals((object?)other);
    }
}

public abstract class ValueObject<T> : ValueObject
{
    public T Value { get; protected set; } = default!;

    public override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value!;
    }
}
