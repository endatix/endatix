using System.Text.Json;
using Endatix.Modules.Reporting.Features.SearchCopy;
using Endatix.Modules.Reporting.Features.SubmissionGrid;

namespace Endatix.Modules.Reporting.Tests.Features.SearchCopy;

/// <summary>
/// The SQL Server rebuild writes each search key into the statement, so a key a form author named is quoted there.
/// </summary>
public sealed class SearchCopyRebuildSqlTests
{
    private const string Table = "[reporting].[FlattenedSubmissions]";
    private const string QuotedKey = "it's \"x\"\\y";
    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

    [Fact]
    public void SqlServer_KeyWithQuotesAndBackslash_EscapesItsJsonPath()
    {
        // Arrange
        var keys = Keys(QuotedKey);

        // Act
        var sql = SearchCopyRebuildSql.SqlServer(Table, keys);

        // Assert
        sql.Should().Contain("""JSON_VALUE(batch.DataJson, N'$."it''s \"x\"\\y"' RETURNING nvarchar(max)) AS a0""");
    }

    [Fact]
    public void SqlServer_KeyWithQuotesAndBackslash_EscapesItsObjectKeyLiteral()
    {
        // Arrange
        var keys = Keys(QuotedKey);

        // Act
        var sql = SearchCopyRebuildSql.SqlServer(Table, keys);

        // Assert
        sql.Should().Contain("""JSON_OBJECT(N'it''s "x"\y': LOWER(answers.a0) ABSENT ON NULL)""");
    }

    [Fact]
    public void SqlServer_NoSearchKeys_WritesAnEmptyObject()
    {
        // Arrange
        var keys = FormSearchKeys.Empty;

        // Act
        var sql = SearchCopyRebuildSql.SqlServer(Table, keys);

        // Assert
        sql.Should().Contain("SET SearchJson = CAST(N'{}' AS json)");
    }

    private static FormSearchKeys Keys(string key) =>
        FormSearchKeys.FromJson(JsonSerializer.Serialize(
            new[] { new SearchKey(key, SearchKeySources.Auto, SubmissionGridFilterTypes.Text, new DateTime(2026, 10, 30, 0, 0, 0, DateTimeKind.Utc)) },
            WebJson));
}
