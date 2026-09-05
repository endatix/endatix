using Endatix.Core.Abstractions;
using Endatix.Infrastructure.Data;
using Endatix.Modules.Reporting.Contracts.Export;
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

    [Theory]
    [InlineData("Npgsql.EntityFrameworkCore.PostgreSQL")]
    [InlineData("Microsoft.EntityFrameworkCore.SqlServer")]
    public void Add_MultipleNewEntities_AssignsDistinctIdsBeforeTracking(string providerName)
    {
        // Arrange — the duplicate-key failure this guards is provider independent: BaseEntity.Id is
        // DatabaseGeneratedOption.None on both, so an unset Id of 0 is a real key on both. Runs at the
        // model level, so it needs no database and covers the SQL Server leg the integration tests skip.
        var optionsBuilder = new DbContextOptionsBuilder<ReportingDbContext>();
        ConfigureProvider(optionsBuilder, providerName);

        using var context = CreateContext(optionsBuilder.Options);

        const long tenantId = 1L;
        ExportFormat[] formats =
        [
            new(tenantId, "CSV", ExportTarget.Submissions, ExportDeliveryFormat.Csv),
            new(tenantId, "JSON", ExportTarget.Submissions, ExportDeliveryFormat.Json),
            new(tenantId, "Codebook", ExportTarget.Codebook, ExportDeliveryFormat.Json)
        ];

        formats.Select(format => format.Id).Should().AllSatisfy(id => id.Should().Be(0));

        // Act
        context.ExportFormats.AddRange(formats);

        // Assert
        var ids = formats.Select(format => format.Id).ToList();
        ids.Should().OnlyHaveUniqueItems();
        ids.Should().AllSatisfy(id => id.Should().BePositive());
    }

    private static void ConfigureProvider(
        DbContextOptionsBuilder<ReportingDbContext> optionsBuilder,
        string providerName)
    {
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
    }

    private static ReportingDbContext CreateContext(
        DbContextOptions<ReportingDbContext> options,
        ITenantContext? tenantContext = null)
    {
        // A real incrementing generator, not a substitute returning a fixed pair: EF caches one model
        // per context type, so the generator handed to the first context built in this assembly is the
        // one every later context generates ids from. A generator that repeats a value would make
        // multi-row Add fail with a duplicate key.
        SequentialIdGenerator idGenerator = new();

        return new ReportingDbContext(
            options,
            idGenerator,
            tenantContext ?? Substitute.For<ITenantContext>(),
            new EfCoreValueGeneratorFactory(idGenerator));
    }

    private sealed class SequentialIdGenerator : IIdGenerator<long>
    {
        private long _current = 1000;

        public long CreateId() => Interlocked.Increment(ref _current);
    }
}
