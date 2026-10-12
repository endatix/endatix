using Endatix.Core.Abstractions;
using Endatix.Modules.Reporting.Domain;
using Endatix.Modules.Reporting.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Modules.Reporting.Tests.Persistence;

public class ReportingDbContextTests
{
    private const string PostgreSql = "PostgreSql";
    private const string SqlServer = "SqlServer";
    private const string PostgreSqlConnectionString = "Host=localhost;Database=reporting_tests";
    private const string SqlServerConnectionString = "Server=(localdb)\\mssqllocaldb;Database=ReportingTests;Trusted_Connection=True";

    [Theory]
    [InlineData(PostgreSql)]
    [InlineData(SqlServer)]
    public void Model_ContainsReportingEntities_ForSupportedProviders(string provider)
    {
        // Arrange
        using var context = CreateContext(provider);

        // Act
        var entityTypes = context.Model.GetEntityTypes().Select(type => type.ClrType).ToList();

        // Assert
        entityTypes.Should().Contain(typeof(FormSchema));
        entityTypes.Should().Contain(typeof(FlattenedSubmission));
        entityTypes.Should().Contain(typeof(ExportFormat));
        entityTypes.Should().Contain(typeof(SurveyTypeExportMapping));
        context.Model.GetDefaultSchema().Should().Be("reporting");
    }

    [Theory]
    [InlineData(
        PostgreSql,
        "\"IsDefault\" = true AND \"SurveyTypeId\" IS NOT NULL AND \"IsDeleted\" = false",
        "\"IsDefault\" = true AND \"SurveyTypeId\" IS NULL AND \"IsDeleted\" = false")]
    [InlineData(
        SqlServer,
        "[IsDefault] = 1 AND [SurveyTypeId] IS NOT NULL AND [IsDeleted] = 0",
        "[IsDefault] = 1 AND [SurveyTypeId] IS NULL AND [IsDeleted] = 0")]
    public void Model_SurveyTypeExportMapping_UsesFilteredUniqueIndexes(
        string provider,
        string typedDefaultFilter,
        string tenantDefaultFilter)
    {
        // Arrange
        using var context = CreateContext(provider);
        var entityType = context.Model.FindEntityType(typeof(SurveyTypeExportMapping));

        // Act
        var indexFilters = entityType!
            .GetIndexes()
            .Where(index => index.IsUnique)
            .Select(index => index.GetFilter())
            .ToList();

        // Assert
        indexFilters.Should().Contain(typedDefaultFilter);
        indexFilters.Should().Contain(tenantDefaultFilter);
        indexFilters.Should().HaveCount(2);
    }

    [Theory]
    [InlineData(PostgreSql, "jsonb")]
    [InlineData(SqlServer, "json")]
    public void Model_JsonColumns_UseOnlyTheirOwnProvidersColumnType(string provider, string jsonColumnType)
    {
        // Arrange
        using var context = CreateContext(provider);

        // Act
        var dataJsonColumnType = context.Model
            .FindEntityType(typeof(FlattenedSubmission))!
            .FindProperty(nameof(FlattenedSubmission.DataJson))!
            .GetColumnType();

        // Assert
        dataJsonColumnType.Should().Be(jsonColumnType);
    }

    [Fact]
    public void Model_PostgreSqlContextConfiguredForSqlServer_ThrowsNotSupportedException()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<ReportingPostgreSqlDbContext>()
            .UseSqlServer(SqlServerConnectionString)
            .Options;
        using var context = new ReportingPostgreSqlDbContext(options, Substitute.For<ITenantContext>());

        // Act
        var buildModel = () => context.Model;

        // Assert
        buildModel.Should().Throw<NotSupportedException>()
            .WithMessage("ReportingPostgreSqlDbContext is mapped for PostgreSQL only*Microsoft.EntityFrameworkCore.SqlServer*");
    }

    [Fact]
    public void Model_SqlServerContextConfiguredForPostgreSql_ThrowsNotSupportedException()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<ReportingSqlServerDbContext>()
            .UseNpgsql(PostgreSqlConnectionString)
            .Options;
        using var context = new ReportingSqlServerDbContext(options, Substitute.For<ITenantContext>());

        // Act
        var buildModel = () => context.Model;

        // Assert
        buildModel.Should().Throw<NotSupportedException>()
            .WithMessage("ReportingSqlServerDbContext is mapped for SQL Server only*Npgsql.EntityFrameworkCore.PostgreSQL*");
    }

    private static ReportingDbContextBase CreateContext(string provider)
    {
        var tenantContext = Substitute.For<ITenantContext>();
        if (provider == SqlServer)
        {
            var sqlServerOptions = new DbContextOptionsBuilder<ReportingSqlServerDbContext>()
                .UseSqlServer(SqlServerConnectionString)
                .Options;
            return new ReportingSqlServerDbContext(sqlServerOptions, tenantContext);
        }

        var postgreSqlOptions = new DbContextOptionsBuilder<ReportingPostgreSqlDbContext>()
            .UseNpgsql(PostgreSqlConnectionString)
            .Options;
        return new ReportingPostgreSqlDbContext(postgreSqlOptions, tenantContext);
    }
}
