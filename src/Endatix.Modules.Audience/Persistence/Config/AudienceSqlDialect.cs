using Endatix.Core.Abstractions;
using Endatix.Core.Entities;
using Endatix.Modules.Audience.Domain;

namespace Endatix.Modules.Audience.Persistence.Config;

/// <summary>
/// What the Audience mapping says differently on each database provider: identifier quoting and the boolean
/// literal in check constraints and index filters, the JSON column type, and the match key's collation.
/// </summary>
/// <remarks>
/// Every index and constraint is declared once, in the shared configurations, which take their SQL fragments from
/// here. A change to an index therefore reaches every provider at once, and a provider supplies only its spelling.
/// The Audience contexts do not scan <c>[ApplyConfigurationFor]</c> classes, so a new difference between providers
/// becomes a member here, not a class under <c>Config/&lt;Provider&gt;/</c>.
/// </remarks>
internal sealed class AudienceSqlDialect
{
    public static readonly AudienceSqlDialect PostgreSql = new()
    {
        OpenQuote = "\"",
        CloseQuote = "\"",
        FalseLiteral = "false",
        JsonColumnType = "jsonb",
    };

    public static readonly AudienceSqlDialect SqlServer = new()
    {
        OpenQuote = "[",
        CloseQuote = "]",
        FalseLiteral = "0",
        JsonColumnType = "json",
        // External ids keep their case as match keys, and SQL Server's default collations ignore case, so the
        // match key compares by code point, as PostgreSQL's default collation does. Emails arrive normalized.
        MatchKeyCollation = "Latin1_General_100_BIN2",
    };

    private AudienceSqlDialect()
    {
    }

    /// <summary>The provider's native JSON column type.</summary>
    public string JsonColumnType { get; private init; } = string.Empty;

    /// <summary>
    /// A collation that makes the member match key compare by code point, or <see langword="null"/> when the
    /// database's default already does.
    /// </summary>
    public string? MatchKeyCollation { get; private init; }

    /// <summary>Index filter that keeps soft-deleted rows out of the index.</summary>
    public string ActiveRowsFilter => $"{Quote(nameof(BaseEntity.IsDeleted))} = {FalseLiteral}";

    /// <summary>
    /// Index filter that keeps only imports that can still change, read from
    /// <see cref="AudienceImportStatusCodes.Unfinished"/>, so the filter and the code share one list.
    /// </summary>
    public string UnfinishedImportsFilter =>
        $"{Quote(nameof(AudienceImport.Status))} IN ({string.Join(", ", AudienceImportStatusCodes.Unfinished.Select(code => $"'{code}'"))})"
        + $" AND {ActiveRowsFilter}";

    /// <summary>Check constraint that refuses a row without a tenant.</summary>
    public string PositiveTenantIdCheck => $"{Quote(nameof(ITenantOwned.TenantId))} > 0";

    private string OpenQuote { get; init; } = string.Empty;

    private string CloseQuote { get; init; } = string.Empty;

    private string FalseLiteral { get; init; } = string.Empty;

    private string Quote(string column) => $"{OpenQuote}{column}{CloseQuote}";
}
