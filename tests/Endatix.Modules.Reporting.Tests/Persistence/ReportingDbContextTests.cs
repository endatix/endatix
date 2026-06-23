using Endatix.Core.Abstractions;
using Endatix.Infrastructure.Data;
using Endatix.Modules.Reporting.Domain;
using Endatix.Modules.Reporting.Persistence;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

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
        entityTypes.Should().Contain(typeof(FormExportSchema));
        entityTypes.Should().Contain(typeof(FlattenedSubmission));
        entityTypes.Should().Contain(typeof(ExportFormat));
        entityTypes.Should().Contain(typeof(SurveyTypeExportMapping));
        context.Model.GetDefaultSchema().Should().Be("reporting");
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
