using Endatix.Infrastructure.Data.Querying;
using Endatix.Persistence.PostgreSql.Querying;
using Endatix.Persistence.SqlServer.Querying;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Endatix.Infrastructure.Tests.Data.Querying;

/// <summary>
/// The SQL each provider's key filter translates to, on a context that maps the JSON functions the way the host does.
/// No connection is opened.
/// </summary>
public sealed class RelationalJsonObjectKeyFilterSqlTests
{
    private static readonly JsonObjectKeyPath DueKey = new(nameof(SearchRow.SearchJson), "due");

    [Fact]
    public void WhereKeyMatches_PostgreSql_PassesTheLowercasedPatternAsAParameter()
    {
        // Arrange
        using var context = SearchContext.PostgreSql();

        // Act
        var sql = new NpgsqlJsonObjectKeyFilter()
            .WhereKeyMatches(context.Rows, nameof(SearchRow.SearchJson), "company", "ACME")
            .ToQueryString();

        // Assert
        sql.Should().Contain("ILIKE @").And.Contain("'%acme%'").And.Contain("jsonb_extract_path_text");
        sql.Should().NotContain("ILIKE '%acme%'");
    }

    [Fact]
    public void WhereKeyMatches_SqlServer_ReadsTheWholeValueAndComparesLowercased()
    {
        // Arrange
        using var context = SearchContext.SqlServer();

        // Act
        var sql = new SqlServerJsonObjectKeyFilter()
            .WhereKeyMatches(context.Rows, nameof(SearchRow.SearchJson), "company", "ACME")
            .ToQueryString();

        // Assert
        sql.Should().Contain("LOWER(JSON_VALUE([r].[SearchJson], N'$.\"company\"' RETURNING nvarchar(max))) LIKE @");
        sql.Should().Contain("N'%acme%'");
    }

    [Fact]
    public void WhereKeyMatches_SqlServerKeyWithAQuote_DoublesItInThePath()
    {
        // Arrange
        using var context = SearchContext.SqlServer();

        // Act
        var sql = new SqlServerJsonObjectKeyFilter()
            .WhereKeyMatches(context.Rows, nameof(SearchRow.SearchJson), "o'brien", "x")
            .ToQueryString();

        // Assert
        sql.Should().Contain("N'$.\"o''brien\"' RETURNING nvarchar(max)");
    }

    [Fact]
    public void WhereKeyAtLeastAndBefore_PostgreSql_CompareByCodePointWithParameters()
    {
        // Arrange
        using var context = SearchContext.PostgreSql();
        NpgsqlJsonObjectKeyFilter filter = new();

        // Act
        var sql = filter.WhereKeyBefore(filter.WhereKeyAtLeast(context.Rows, DueKey, "2026-03-01"), DueKey, "2026-04-01")
            .ToQueryString();

        // Assert
        sql.Should().Contain("COLLATE \"C\" >= @").And.Contain("COLLATE \"C\" < @");
        sql.Should().Contain("'2026-03-01'").And.Contain("'2026-04-01'");
    }

    [Fact]
    public void WhereKeyAtLeastAndBefore_SqlServer_CompareByCodePointWithParameters()
    {
        // Arrange
        using var context = SearchContext.SqlServer();
        SqlServerJsonObjectKeyFilter filter = new();

        // Act
        var sql = filter.WhereKeyBefore(filter.WhereKeyAtLeast(context.Rows, DueKey, "2026-03-01"), DueKey, "2026-04-01")
            .ToQueryString();

        // Assert
        sql.Should().Contain("COLLATE Latin1_General_100_BIN2 >= @").And.Contain("COLLATE Latin1_General_100_BIN2 < @");
    }

    public sealed class SearchRow
    {
        public long Id { get; set; }

        public string? SearchJson { get; set; }
    }

    private sealed class SearchContext(DbContextOptions<SearchContext> options) : DbContext(options)
    {
        public DbSet<SearchRow> Rows => Set<SearchRow>();

        public static SearchContext PostgreSql()
        {
            DbContextOptionsBuilder<SearchContext> builder = new();
            builder.UseNpgsql("Host=127.0.0.1;Database=not_connected;Username=x;Password=x");
            builder.ReplaceService<IModelCustomizer, NpgsqlEndatixModelCustomizer>();
            return new SearchContext(builder.Options);
        }

        public static SearchContext SqlServer()
        {
            DbContextOptionsBuilder<SearchContext> builder = new();
            builder.UseSqlServer("Server=127.0.0.1;Database=not_connected;User Id=x;Password=x");
            builder.ReplaceService<IModelCustomizer, SqlServerEndatixModelCustomizer>();
            return new SearchContext(builder.Options);
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder) =>
            modelBuilder.Entity<SearchRow>().ToTable("Rows");
    }
}
