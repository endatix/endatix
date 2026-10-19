using Endatix.Modules.Reporting.Features.SubmissionGrid;

namespace Endatix.Modules.Reporting.Features.SearchCopy;

/// <summary>
/// The statement that rebuilds one batch of a form's search copies from their stored answers, per provider. It writes
/// what <see cref="SearchCopyDocument"/> writes at flatten time, with the date rules of <see cref="SearchValues"/>.
/// Text is lowercased by the database (<c>lower()</c> / <c>LOWER</c>), which follows its collation, where the flatten
/// lowercases with .NET's invariant rules.
/// </summary>
/// <remarks>
/// <para>
/// Parameters: <c>@tenantId</c>, <c>@formId</c>, <c>@afterId</c> (the last submission id of the previous batch),
/// <c>@batchSize</c>, <c>@revision</c> (the form's search keys revision), and on PostgreSQL <c>@keys</c>, a JSON object
/// of search key to filter type (SQL Server writes the keys into the statement). The statement updates the next
/// <c>@batchSize</c> processed rows by submission id and returns the last id it updated and how many it did, or a
/// null id when no row was left.
/// </para>
/// <para>
/// Only rows stamped with an older revision are rewritten, and each is stamped with <c>@revision</c>. A rebuild that
/// was cut off therefore resumes where it stopped, and one that read older keys never overwrites a newer row.
/// </para>
/// <para>
/// Date shapes are matched with a binary collation on SQL Server, so <c>[0-9]</c> means ASCII digits whatever the
/// database collation.
/// </para>
/// </remarks>
internal static class SearchCopyRebuildSql
{
    private const string IsoDate = "[0-9][0-9][0-9][0-9]-[0-9][0-9]-[0-9][0-9]";
    private const string IsoTime = "[0-9][0-9]:[0-9][0-9]";
    private const string SqlServerBinary = "COLLATE Latin1_General_100_BIN2";

    public static string PostgreSql(string table) => $$"""
        WITH batch AS (
            SELECT "SubmissionId" FROM {{table}}
            WHERE "TenantId" = @tenantId AND "FormId" = @formId AND "SubmissionId" > @afterId AND "DataJson" IS NOT NULL
              AND "SearchKeysRevision" < @revision
            ORDER BY "SubmissionId"
            LIMIT @batchSize),
        updated AS (
            UPDATE {{table}} AS f
            SET "SearchJson" = {{PostgreSqlSearchCopy()}}, "SearchKeysRevision" = @revision
            FROM batch
            WHERE f."SubmissionId" = batch."SubmissionId" AND f."SearchKeysRevision" < @revision
            RETURNING f."SubmissionId")
        SELECT max("SubmissionId"), count(*) FROM updated;
        """;

    // The answered search keys of the row aliased f, normalised; an empty object when none is answered.
    private static string PostgreSqlSearchCopy() => $$"""
        COALESCE((
            SELECT jsonb_object_agg(k.key, to_jsonb(CASE k.filter_type
                WHEN 'date' THEN {{PostgreSqlDate("answer.text")}}
                WHEN 'datetime' THEN {{PostgreSqlDateTime("answer.text")}}
                ELSE lower(answer.text) END))
            FROM jsonb_each_text(CAST(@keys AS jsonb)) AS k(key, filter_type)
            CROSS JOIN LATERAL (SELECT f."DataJson" -> k.key AS value, f."DataJson" ->> k.key AS text) AS answer
            WHERE jsonb_typeof(answer.value) IN ('string', 'number', 'boolean')), '{}'::jsonb)
        """;

    // Each key is read with its own JSON_VALUE: shredding the whole answers document with OPENJSON costs about
    // 30 ms per row on a 1,000-question form. The keys are written into the statement, quoted.
    public static string SqlServer(string table, FormSearchKeys searchKeys) => $$"""
        DECLARE @updated TABLE (SubmissionId bigint NOT NULL);
        WITH batch AS (
            SELECT TOP (@batchSize) SubmissionId, DataJson, SearchJson, SearchKeysRevision FROM {{table}}
            WHERE TenantId = @tenantId AND FormId = @formId AND SubmissionId > @afterId AND DataJson IS NOT NULL
              AND SearchKeysRevision < @revision
            ORDER BY SubmissionId)
        UPDATE batch
        SET SearchJson = CAST({{SqlServerSearchCopy(searchKeys)}} AS json), SearchKeysRevision = @revision
        OUTPUT inserted.SubmissionId INTO @updated;
        SELECT MAX(SubmissionId), COUNT(*) FROM @updated;
        """;

    private static string SqlServerSearchCopy(FormSearchKeys searchKeys)
    {
        if (searchKeys.Keys.Count == 0)
        {
            return "N'{}'";
        }

        var values = searchKeys.Keys.Select((key, index) =>
            $"JSON_VALUE(batch.DataJson, {SqlServerLiteral(SqlServerJsonPath(key.Key))} RETURNING nvarchar(max)) AS a{index}");
        var pairs = searchKeys.Keys.Select((key, index) =>
            $"{SqlServerLiteral(key.Key)}: {SqlServerNormalized($"answers.a{index}", key.FilterType)}");
        return $"(SELECT JSON_OBJECT({string.Join(", ", pairs)} ABSENT ON NULL) FROM (SELECT {string.Join(", ", values)}) AS answers)";
    }

    private static string SqlServerNormalized(string text, string filterType) => filterType switch
    {
        SubmissionGridFilterTypes.Date => SqlServerDate(text),
        SubmissionGridFilterTypes.DateTime => SqlServerDateTime(text),
        _ => $"LOWER({text})",
    };

    private static string SqlServerJsonPath(string key) =>
        "$.\"" + key.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";

    private static string SqlServerLiteral(string value) => $"N'{value.Replace("'", "''", StringComparison.Ordinal)}'";

    private static string PostgreSqlDate(string text) =>
        $"CASE WHEN {text} ~ '^[0-9]{{4}}-[0-9]{{2}}-[0-9]{{2}}' THEN left({text}, 10) ELSE {text} END";

    private static string PostgreSqlDateTime(string text) => $$"""
        CASE
            WHEN {{text}} ~ '^[0-9]{4}-[0-9]{2}-[0-9]{2}[Tt ][0-9]{2}:[0-9]{2}:[0-9]{2}' THEN left({{text}}, 10) || 'T' || substr({{text}}, 12, 8)
            WHEN {{text}} ~ '^[0-9]{4}-[0-9]{2}-[0-9]{2}[Tt ][0-9]{2}:[0-9]{2}' THEN left({{text}}, 10) || 'T' || substr({{text}}, 12, 5) || ':00'
            WHEN {{text}} ~ '^[0-9]{4}-[0-9]{2}-[0-9]{2}$' THEN {{text}} || 'T00:00:00'
            ELSE {{text}}
        END
        """;

    private static string SqlServerDate(string text) =>
        $"CASE WHEN {text} {SqlServerBinary} LIKE N'{IsoDate}%' THEN LEFT({text}, 10) ELSE {text} END";

    private static string SqlServerDateTime(string text) => $$"""
        CASE
            WHEN {{text}} {{SqlServerBinary}} LIKE N'{{IsoDate}}[Tt ]{{IsoTime}}:[0-9][0-9]%' THEN LEFT({{text}}, 10) + N'T' + SUBSTRING({{text}}, 12, 8)
            WHEN {{text}} {{SqlServerBinary}} LIKE N'{{IsoDate}}[Tt ]{{IsoTime}}%' THEN LEFT({{text}}, 10) + N'T' + SUBSTRING({{text}}, 12, 5) + N':00'
            WHEN {{text}} {{SqlServerBinary}} LIKE N'{{IsoDate}}' THEN {{text}} + N'T00:00:00'
            ELSE {{text}}
        END
        """;
}
