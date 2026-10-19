using System.Text.RegularExpressions;

namespace Endatix.Infrastructure.Data;

/// <summary>
/// Guards database object names that are composed into raw SQL. Identifiers cannot be bound as
/// parameters, so a name is accepted only when it cannot carry anything but the name itself.
/// </summary>
public static partial class SqlIdentifier
{
    /// <summary>
    /// An unquoted identifier (<c>export_form_submissions</c>), optionally schema-qualified (<c>reporting.export_rows</c>).
    /// </summary>
    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_]*(\\.[A-Za-z_][A-Za-z0-9_]*)?$", RegexOptions.CultureInvariant)]
    private static partial Regex IdentifierPattern { get; }

    /// <summary>
    /// Returns <paramref name="name"/> when it is a plain SQL identifier.
    /// </summary>
    /// <exception cref="ArgumentException">The name holds anything but letters, digits, underscores and one schema dot.</exception>
    public static string EnsureValid(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (!IdentifierPattern.IsMatch(name))
        {
            throw new ArgumentException($"'{name}' is not a valid SQL identifier.", nameof(name));
        }

        return name;
    }
}
