using Endatix.Core.Abstractions;
using Endatix.Infrastructure.Data;
using Endatix.Modules.Reporting.Domain;
using Endatix.Modules.Reporting.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Modules.Reporting.Tests.Persistence;

public class ReportingDbContextTests
{
    [Theory]
    [InlineData("Npgsql.EntityFrameworkCore.PostgreSQL")]
    [InlineData("Microsoft.EntityFrameworkCore.SqlServer")]
    public void Model_ContainsReportingEntities_ForSupportedProviders(string providerName)
    {
        // Arrange
        var optionsBuilder = new DbContextOptionsBuilder<ReportingDbContext>();
        if (providerName.Contains("SqlServer", StringComparison.Ordinal))
        {
            optionsBuilder.UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=ReportingTests;Trusted_Connection=True");
            ModuleDbContextExtensions.ConfigureProviderScopedMigrations(
                optionsBuilder,
                ReportingPersistence.SqlServerMigrationsNamespace);
        }
        else
        {
            optionsBuilder.UseNpgsql("Host=localhost;Database=reporting_tests;Username=postgres;Password=postgres");
            ModuleDbContextExtensions.ConfigureProviderScopedMigrations(
                optionsBuilder,
                ReportingPersistence.PostgreSqlMigrationsNamespace);
        }

        using var context = CreateContext(optionsBuilder.Options);

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
        "Npgsql.EntityFrameworkCore.PostgreSQL",
        "\"IsDefault\" = true AND \"SurveyTypeId\" IS NOT NULL",
        "\"IsDefault\" = true AND \"SurveyTypeId\" IS NULL")]
    [InlineData(
        "Microsoft.EntityFrameworkCore.SqlServer",
        "[IsDefault] = 1 AND [SurveyTypeId] IS NOT NULL",
        "[IsDefault] = 1 AND [SurveyTypeId] IS NULL")]
    public void Model_SurveyTypeExportMapping_UsesFilteredUniqueIndexes(
        string providerName,
        string typedDefaultFilter,
        string tenantDefaultFilter)
    {
        // Arrange
        var optionsBuilder = new DbContextOptionsBuilder<ReportingDbContext>();
        if (providerName.Contains("SqlServer", StringComparison.Ordinal))
        {
            optionsBuilder.UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=ReportingTests;Trusted_Connection=True");
            ModuleDbContextExtensions.ConfigureProviderScopedMigrations(
                optionsBuilder,
                ReportingPersistence.SqlServerMigrationsNamespace);
        }
        else
        {
            optionsBuilder.UseNpgsql("Host=localhost;Database=reporting_tests;Username=postgres;Password=postgres");
            ModuleDbContextExtensions.ConfigureProviderScopedMigrations(
                optionsBuilder,
                ReportingPersistence.PostgreSqlMigrationsNamespace);
        }

        using var context = CreateContext(optionsBuilder.Options);
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

    private static ReportingDbContext CreateContext(
        DbContextOptions<ReportingDbContext> options,
        ITenantContext? tenantContext = null)
    {
        var idGenerator = Substitute.For<IIdGenerator<long>>();
        idGenerator.CreateId().Returns(1001, 1002);

        return new ReportingDbContext(
            options,
            idGenerator,
            tenantContext ?? Substitute.For<ITenantContext>());
    }
}
