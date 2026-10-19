using System.Linq.Expressions;
using Endatix.Infrastructure.Data.Querying;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Persistence.SqlServer.Querying;

/// <summary>
/// SQL Server submitter profile filter: <c>JSON_VALUE</c> on the snapshot, compared under a binary collation.
/// </summary>
/// <remarks>
/// The binary collation keeps matching case-sensitive, as PostgreSQL's <c>@&gt;</c> is, whatever the database
/// collation. SQL Server still ignores trailing spaces when it compares strings.
/// </remarks>
public sealed class SqlServerSubmitterProfileFilter : RelationalSubmitterProfileFilterBase
{
    private const string ExactCollation = "Latin1_General_100_BIN2";

    /// <inheritdoc />
    protected override Expression<Func<string?, bool>> HasAnyOf(string field, List<string> values)
    {
        var path = JsonPathOf(field);
        IEnumerable<string?> candidates = values;
        return snapshot => snapshot != null &&
                           candidates.Contains(EF.Functions.Collate(
                               SqlServerJsonDbFunctions.JsonValue(snapshot, path),
                               ExactCollation));
    }

    /// <inheritdoc />
    protected override Expression<Func<string?, bool>> HasNoneOf(string field, List<string> values)
    {
        var path = JsonPathOf(field);
        IEnumerable<string?> candidates = values;
        return snapshot => snapshot == null ||
                           SqlServerJsonDbFunctions.JsonValue(snapshot, path) == null ||
                           !candidates.Contains(EF.Functions.Collate(
                               SqlServerJsonDbFunctions.JsonValue(snapshot, path),
                               ExactCollation));
    }

    // Quoted, so a field name with a dot or a space is one key, not a nested path.
    private static string JsonPathOf(string field) =>
        $"$.\"{field.Replace("\\", "\\\\").Replace("\"", "\\\"")}\"";
}
