using Ardalis.GuardClauses;

namespace Endatix.Core.Entities.Identity;

/// <summary>
/// Value Object representing permission categories in the system.
/// Categories are used to group related permissions for better organization and management.
/// </summary>
public sealed record PermissionCategory : IComparable<PermissionCategory>
{
    public const int CATEGORY_CODE_MAX_LENGTH = 16;

    // Required by EF Core
    private PermissionCategory()
    {
        Name = string.Empty;
        Code = string.Empty;
    }

    private PermissionCategory(string name, string code)
    {
        Guard.Against.NullOrWhiteSpace(name);
        Guard.Against.NullOrWhiteSpace(code);

        if (code.Length > CATEGORY_CODE_MAX_LENGTH)
        {
            throw new ArgumentException($"Code cannot be longer than {CATEGORY_CODE_MAX_LENGTH} characters", nameof(code));
        }

        Name = name;
        Code = code.ToLowerInvariant();
    }

    // System-defined categories
    public static readonly PermissionCategory Access = new("Access", "access");
    public static readonly PermissionCategory Platform = new("Platform Management", "platform");
    public static readonly PermissionCategory Tenant = new("Tenant Management", "tenant");
    public static readonly PermissionCategory Forms = new("Forms", "forms");
    public static readonly PermissionCategory Folders = new("Folders", "folders");
    public static readonly PermissionCategory Templates = new("Templates", "templates");
    public static readonly PermissionCategory Themes = new("Themes", "themes");
    public static readonly PermissionCategory Submissions = new("Submissions", "submissions");
    public static readonly PermissionCategory Questions = new("Questions", "questions");
    public static readonly PermissionCategory Custom = new("Custom", "custom");

    public string Name { get; }
    public string Code { get; }

    /// <summary>
    /// Creates a PermissionCategory from a code string
    /// </summary>
    /// <param name="code">The category code</param>
    /// <returns>The corresponding PermissionCategory</returns>
    /// <exception cref="ArgumentException">Thrown when the code is invalid</exception>
    public static PermissionCategory FromCode(string code)
    {
        Guard.Against.NullOrWhiteSpace(code);

        var category = code.ToLowerInvariant() switch
        {
            "access" => Access,
            "platform" => Platform,
            "tenant" => Tenant,
            "forms" => Forms,
            "folders" => Folders,
            "templates" => Templates,
            "themes" => Themes,
            "submissions" => Submissions,
            "questions" => Questions,
            "custom" => Custom,
            _ => throw new ArgumentException($"Invalid category code: {code}", nameof(code))
        };

        return category;
    }

    /// <summary>
    /// Gets all available permission categories
    /// </summary>
    /// <returns>Collection of all permission categories</returns>
    public static IEnumerable<PermissionCategory> GetAll()
    {
        return new[]
        {
            Access,
            Platform,
            Tenant,
            Forms,
            Folders,
            Templates,
            Themes,
            Submissions,
            Questions,
            Custom,
        };
    }

    public override string ToString() => Name;

    public int CompareTo(PermissionCategory? other)
    {
        if (other is null)
        {
            return 1;
        }

        return string.Compare(Code, other.Code, StringComparison.Ordinal);
    }

    public static bool operator <(PermissionCategory? left, PermissionCategory? right)
    {
        if (left is null)
        {
            return right is not null;
        }

        return left.CompareTo(right) < 0;
    }

    public static bool operator <=(PermissionCategory? left, PermissionCategory? right)
    {
        if (left is null)
        {
            return true;
        }

        return left.CompareTo(right) <= 0;
    }

    public static bool operator >(PermissionCategory? left, PermissionCategory? right) => !(left <= right);

    public static bool operator >=(PermissionCategory? left, PermissionCategory? right) => !(left < right);
}
