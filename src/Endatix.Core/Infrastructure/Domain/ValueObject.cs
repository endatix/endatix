using System;
using System.Collections.Generic;
using System.Linq;

namespace Endatix.Core.Infrastructure.Domain;

/// <summary>
/// NOTE: Use `readonly record struct` for most cases in C# 10+
/// See: https://nietras.com/2021/06/14/csharp-10-record-struct/
/// For this class implementation, reference:
/// See: https://enterprisecraftsmanship.com/posts/value-object-better-implementation/
/// </summary>
[Serializable]
public abstract class ValueObject : IComparable, IComparable<ValueObject>
{
    protected abstract IEnumerable<object> GetEqualityComponents();

    public override bool Equals(object? obj)
    {
        if (obj == null)
        {
            return false;
        }

        if (GetUnproxiedType(this) != GetUnproxiedType(obj))
        {
            return false;
        }

        var valueObject = (ValueObject)obj;

        return GetEqualityComponents().SequenceEqual(valueObject.GetEqualityComponents());
    }

    public override int GetHashCode()
    {
        // No cache: a cached field is mutable and trips S2328; value objects are small.
        return GetEqualityComponents()
            .Aggregate(1, (current, obj) =>
            {
                unchecked
                {
                    return (current * 23) + (obj?.GetHashCode() ?? 0);
                }
            });
    }

    public int CompareTo(object? obj)
    {
        if (obj == null)
        {
            return 1;
        }

        var thisType = GetUnproxiedType(this);
        var otherType = GetUnproxiedType(obj);

        if (thisType != otherType)
        {
            return string.Compare(thisType.ToString(), otherType.ToString(), StringComparison.Ordinal);
        }

        return CompareEqualityComponents((ValueObject)obj);
    }

    /// <inheritdoc/>
    public int CompareTo(ValueObject? other)
    {
        return CompareTo(other as object);
    }

    private int CompareEqualityComponents(ValueObject other)
    {
        var components = GetEqualityComponents().ToArray();
        var otherComponents = other.GetEqualityComponents().ToArray();

        for (var i = 0; i < components.Length; i++)
        {
            var comparison = CompareComponents(components[i], otherComponents[i]);
            if (comparison != 0)
            {
                return comparison;
            }
        }

        return 0;
    }

    /// <summary>Null sorts before any value; otherwise compares by <c>CompareTo</c>.</summary>
    public static bool operator <(ValueObject? left, ValueObject? right) => ComparisonOperators.LessThan(left, right);

    /// <summary>Null sorts before any value; otherwise compares by <c>CompareTo</c>.</summary>
    public static bool operator <=(ValueObject? left, ValueObject? right) => ComparisonOperators.LessThanOrEqual(left, right);

    /// <summary>Inverse of <c>&lt;=</c>.</summary>
    public static bool operator >(ValueObject? left, ValueObject? right) => !(left <= right);

    /// <summary>Inverse of <c>&lt;</c>.</summary>
    public static bool operator >=(ValueObject? left, ValueObject? right) => !(left < right);

    private static int CompareComponents(object? object1, object? object2)
    {
        if (object1 is IComparable comparable1 && object2 is IComparable comparable2)
        {
            return comparable1.CompareTo(comparable2);
        }

        if (object1 is null || object2 is null)
        {
            return (object1 is null ? 0 : 1) - (object2 is null ? 0 : 1);
        }

        return object1.Equals(object2) ? 0 : -1;
    }

    /// <summary>Value equality; two nulls are equal.</summary>
    public static bool operator ==(ValueObject? a, ValueObject? b)
    {
        if (a is null && b is null)
        {
            return true;
        }

        if (a is null || b is null)
        {
            return false;
        }

        return a.Equals(b);
    }

    /// <summary>Negation of <c>==</c>.</summary>
    public static bool operator !=(ValueObject? a, ValueObject? b)
    {
        return !(a == b);
    }

    internal static Type GetUnproxiedType(object obj)
    {
        ArgumentNullException.ThrowIfNull(obj);

        const string EFCoreProxyPrefix = "Castle.Proxies.";
        const string NHibernateProxyPostfix = "Proxy";

        var type = obj.GetType();
        var typeString = type.ToString();

        if (typeString.Contains(EFCoreProxyPrefix) || typeString.EndsWith(NHibernateProxyPostfix))
        {
            return type.BaseType!;
        }

        return type;
    }
}
