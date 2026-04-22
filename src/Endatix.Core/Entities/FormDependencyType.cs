using Ardalis.GuardClauses;
using System.ComponentModel.DataAnnotations.Schema;

namespace Endatix.Core.Entities;



/// <summary>
/// Value object describing supported form dependency kinds.
/// </summary>
[ComplexType]
public sealed record FormDependencyType : IComparable<FormDependencyType>
{
    public const int TYPE_CODE_MAX_LENGTH = 32;

    private FormDependencyType()
    {
        Name = string.Empty;
        Code = string.Empty;
    }

    /// <summary>
    /// The data list dependency type allowing to track which data lists are used in the form.
    /// </summary>
    public static readonly FormDependencyType DataList = new("Data List", Codes.DataList);

    public string Name { get; }
    public string Code { get; }

    private FormDependencyType(string name, string code)
    {
        Guard.Against.NullOrWhiteSpace(name);
        Guard.Against.NullOrWhiteSpace(code, nameof(code));

        if (code.Length > TYPE_CODE_MAX_LENGTH)
        {
            throw new ArgumentException($"Code cannot be longer than {TYPE_CODE_MAX_LENGTH} characters", nameof(code));
        }

        Name = name;
        Code = code.ToLowerInvariant();
    }

    public static FormDependencyType FromCode(string code)
    {
        Guard.Against.NullOrWhiteSpace(code, nameof(code));

        return code.ToLowerInvariant() switch
        {
            Codes.DataList => DataList,
            _ => throw new ArgumentException($"Invalid dependency type code: {code}", nameof(code))
        };
    }

    public static IReadOnlyCollection<FormDependencyType> GetAll() => [DataList];

    public int CompareTo(FormDependencyType? other)
    {
        if (other is null)
        {
            return 1;
        }

        return string.Compare(Code, other.Code, StringComparison.Ordinal);
    }

    public override string ToString() => Name;


    internal static class Codes
    {
        public const string DataList = "datalist";
    }
}
