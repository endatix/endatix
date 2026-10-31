using System.Text.RegularExpressions;

namespace Endatix.IntegrationTests;

/// <summary>Statements of the audience module's background work that a test pauses at, on either provider.</summary>
internal static partial class AudienceStatements
{
    /// <summary>
    /// The form-deletion cleanup's soft delete of the form's memberships, its second statement, after the one that
    /// soft-deleted their value cells. PostgreSQL writes <c>UPDATE audience."Memberships" AS m</c>; SQL Server
    /// <c>UPDATE [m] SET … FROM [audience].[Memberships] AS [m]</c>, whose alias tells it from the first statement,
    /// which reads the memberships in a subquery.
    /// </summary>
    [GeneratedRegex("""\A\s*UPDATE (audience\."Memberships"|\[(?<alias>\w+)\]\s+SET\b.*?\bFROM \[audience\]\.\[Memberships\] AS \[\k<alias>\])""", RegexOptions.Singleline)]
    public static partial Regex MembershipsSoftDelete();

    /// <summary>The test-mark handler's clearing of the link, after it locked the submission's row.</summary>
    [GeneratedRegex("""\A\s*UPDATE .*["\[]Links["\]]""", RegexOptions.Singleline)]
    public static partial Regex LinkRelease();

    /// <summary>An import Check's save of the counts of a chunk it has checked.</summary>
    [GeneratedRegex("""UPDATE\s+["\[]?audience["\]]?\.["\[]Imports["\]].*["\[]CheckedRowCount["\]]\s*=""", RegexOptions.Singleline)]
    public static partial Regex ImportCheckProgressSave();
}
