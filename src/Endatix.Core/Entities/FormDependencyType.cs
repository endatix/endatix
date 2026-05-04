using Ardalis.GuardClauses;
using System.ComponentModel.DataAnnotations.Schema;

namespace Endatix.Core.Entities;

/// <summary>
/// Value object describing supported form dependency kinds.
/// </summary>
[ComplexType]
public sealed class FormDependencyType : IComparable<FormDependencyType>, IEquatable<FormDependencyType>
{
    /// <summary>
    /// The maximum length of the code of the dependency type.
    /// </summary>
    public const int TYPE_CODE_MAX_LENGTH = 32;

    private FormDependencyType()
    {
        Name = string.Empty;
        Code = string.Empty;
    }

    /// <summary>
    /// Creates a new dependency type.
    /// </summary>
    /// <param name="name">The name of the dependency type.</param>
    /// <param name="code">The code of the dependency type.</param>
    private FormDependencyType(string name, string code)
    {
        Guard.Against.NullOrWhiteSpace(name);
        Guard.Against.NullOrWhiteSpace(code);

        if (code.Length > TYPE_CODE_MAX_LENGTH)
        {
            throw new ArgumentException($"Code cannot be longer than {TYPE_CODE_MAX_LENGTH} characters");
        }

        Name = name;
        Code = code.ToLowerInvariant();
    }

    /// <summary>
    /// The name of the dependency type.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// The code of the dependency type.
    /// </summary>
    public string Code { get; }

    /// <summary>
    /// The data list dependency type allowing to track which data lists are used in the form.
    /// </summary>
    public static readonly FormDependencyType DataList = new("Data List", FormDependencyCodes.DataList);

    /// <summary>
    /// Returns the dependency type for a given code.
    /// </summary>
    /// <param name="code">The code of the dependency type.</param>
    /// <returns>The dependency type.</returns>
    public static FormDependencyType FromCode(string code)
    {
        Guard.Against.NullOrWhiteSpace(code);

        return code.ToLowerInvariant() switch
        {
            FormDependencyCodes.DataList => DataList,
            _ => throw new ArgumentException($"Invalid dependency type code: {code}")
        };
    }

    /// <summary>
    /// Returns all available dependency types.
    /// </summary>
    /// <returns>A collection of all available dependency types.</returns>
    public static IReadOnlyCollection<FormDependencyType> GetAll() => [DataList];

    /// <summary>
    /// Compares the current dependency type to another dependency type.
    /// </summary>
    /// <param name="other">The other dependency type to compare to.</param>
    /// <returns>A negative integer, zero, or a positive integer as the current dependency type is less than, equal to, or greater than the other dependency type.</returns>
    public int CompareTo(FormDependencyType? other)
    {
        if (other is null)
        {
            return 1;
        }

        return string.Compare(Code, other.Code, StringComparison.Ordinal);
    }

    /// <inheritdoc />
    public bool Equals(FormDependencyType? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return string.Equals(Code, other.Code, StringComparison.Ordinal);
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as FormDependencyType);

    /// <inheritdoc />
    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Code);

    /// <summary>
    /// Returns the name of the dependency type.
    /// </summary>
    /// <returns>The name of the dependency type.</returns>
    public override string ToString() => Name;

    /// <summary>
    /// Compares two instances for ordering; null is ordered before non-null.
    /// </summary>
    private static int Compare(FormDependencyType? left, FormDependencyType? right)
    {
        if (ReferenceEquals(left, right))
        {
            return 0;
        }

        if (left is null)
        {
            return -1;
        }

        return left.CompareTo(right);
    }

    /// <summary>Determines whether two instances have the same dependency type code.</summary>
    public static bool operator ==(FormDependencyType? left, FormDependencyType? right) => left?.Equals(right) ?? (right is null);

    /// <summary>Determines whether two instances do not represent the same dependency type.</summary>
    public static bool operator !=(FormDependencyType? left, FormDependencyType? right) => !(left == right);

    /// <summary>Less-than comparison based on <see cref="Code"/> (ordinal).</summary>
    public static bool operator <(FormDependencyType? left, FormDependencyType? right) => Compare(left, right) < 0;

    /// <summary>Greater-than comparison based on <see cref="Code"/> (ordinal).</summary>
    public static bool operator >(FormDependencyType? left, FormDependencyType? right) => Compare(left, right) > 0;

    /// <summary>Less-than-or-equal comparison based on <see cref="Code"/> (ordinal).</summary>
    public static bool operator <=(FormDependencyType? left, FormDependencyType? right) => Compare(left, right) <= 0;

    /// <summary>Greater-than-or-equal comparison based on <see cref="Code"/> (ordinal).</summary>
    public static bool operator >=(FormDependencyType? left, FormDependencyType? right) => Compare(left, right) >= 0;
}

/// <summary>
/// Static class containing the codes for the form dependency types.
/// </summary>
internal static class FormDependencyCodes
{
    /// <summary>
    /// The code for the data list dependency type.
    /// </summary>
    /// <returns>The code for the data list dependency type.</returns>
    internal const string DataList = "datalist";
}
