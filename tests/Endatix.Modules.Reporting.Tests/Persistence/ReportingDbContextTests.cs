using Endatix.Core.Abstractions;
using Endatix.Infrastructure.Data;
using Endatix.Modules.Reporting.Domain;
using Endatix.Modules.Reporting.Persistence;
using Microsoft.Data.Sqlite;
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
        entityTypes.Should().Contain(typeof(FormExportSchema));
        entityTypes.Should().Contain(typeof(FlattenedSubmission));
        entityTypes.Should().Contain(typeof(ExportFormat));
        entityTypes.Should().Contain(typeof(SurveyTypeExportMapping));
        context.Model.GetDefaultSchema().Should().Be("reporting");
    }

    [Theory]
    [InlineData("Npgsql.EntityFrameworkCore.PostgreSQL", "\"SurveyTypeId\" IS NOT NULL", "\"SurveyTypeId\" IS NULL")]
    [InlineData("Microsoft.EntityFrameworkCore.SqlServer", "[SurveyTypeId] IS NOT NULL", "[SurveyTypeId] IS NULL")]
    public void Model_SurveyTypeExportMapping_UsesFilteredUniqueIndexes(
        string providerName,
        string typedMappingFilter,
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
        indexFilters.Should().Contain(typedMappingFilter);
        indexFilters.Should().Contain(tenantDefaultFilter);
    }

    [Fact]
    public void ApplyProviderSpecificConfigurations_WithUnsupportedProvider_ThrowsNotSupportedException()
    {
        // Arrange
        var optionsBuilder = new DbContextOptionsBuilder<ReportingDbContext>();
        optionsBuilder.UseSqlite("DataSource=:memory:");

        using var context = CreateContext(optionsBuilder.Options);

        // Act
        var action = () => _ = context.Model;

        // Assert
        action.Should().Throw<NotSupportedException>()
            .WithMessage("*not supported*");
    }

    [Fact]
    public async Task QueryFilters_EnforceTenantIsolation_ForAllReportingEntities()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        var idGenerator = new IncrementingIdGenerator();
        var bypassTenant = Substitute.For<ITenantContext>();
        bypassTenant.TenantId.Returns(0L);

        const long tenant1 = 1;
        const long tenant2 = 2;

        using (var seedContext = CreateTestContext(connection, idGenerator, bypassTenant))
        {
            await seedContext.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

            seedContext.ExportFormats.AddRange(
                new ExportFormat(tenant1, "Tenant1 Export", ExportSerializationType.Csv) { Id = 101 },
                new ExportFormat(tenant2, "Tenant2 Export", ExportSerializationType.Json) { Id = 102 });

            seedContext.FormExportSchemas.AddRange(
                new FormExportSchema(tenant1, formId: 1, formDefinitionRevision: 1, schemaJson: """[{"col":"a"}]""") { Id = 201 },
                new FormExportSchema(tenant2, formId: 2, formDefinitionRevision: 1, schemaJson: """[{"col":"b"}]""") { Id = 202 });

            seedContext.FlattenedSubmissions.AddRange(
                new FlattenedSubmission(submissionId: 301, tenantId: tenant1, formId: 1),
                new FlattenedSubmission(submissionId: 302, tenantId: tenant2, formId: 2));

            seedContext.SurveyTypeExportMappings.AddRange(
                new SurveyTypeExportMapping(tenant1, exportFormatId: 101) { Id = 401 },
                new SurveyTypeExportMapping(tenant2, exportFormatId: 102) { Id = 402 });

            await seedContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var tenant1Ctx = Substitute.For<ITenantContext>();
        tenant1Ctx.TenantId.Returns(tenant1);
        using (var ctx = CreateTestContext(connection, idGenerator, tenant1Ctx))
        {
            (await ctx.ExportFormats.ToListAsync(cancellationToken: TestContext.Current.CancellationToken)).Should().ContainSingle().Which.TenantId.Should().Be(tenant1);
            (await ctx.FormExportSchemas.ToListAsync(cancellationToken: TestContext.Current.CancellationToken)).Should().ContainSingle().Which.TenantId.Should().Be(tenant1);
            (await ctx.FlattenedSubmissions.ToListAsync(cancellationToken: TestContext.Current.CancellationToken)).Should().ContainSingle().Which.TenantId.Should().Be(tenant1);
            (await ctx.SurveyTypeExportMappings.ToListAsync(cancellationToken: TestContext.Current.CancellationToken)).Should().ContainSingle().Which.TenantId.Should().Be(tenant1);
        }

        var tenant2Ctx = Substitute.For<ITenantContext>();
        tenant2Ctx.TenantId.Returns(tenant2);
        using (var ctx = CreateTestContext(connection, idGenerator, tenant2Ctx))
        {
            (await ctx.ExportFormats.ToListAsync(cancellationToken: TestContext.Current.CancellationToken)).Should().ContainSingle().Which.TenantId.Should().Be(tenant2);
            (await ctx.FormExportSchemas.ToListAsync(cancellationToken: TestContext.Current.CancellationToken)).Should().ContainSingle().Which.TenantId.Should().Be(tenant2);
            (await ctx.FlattenedSubmissions.ToListAsync(cancellationToken: TestContext.Current.CancellationToken)).Should().ContainSingle().Which.TenantId.Should().Be(tenant2);
            (await ctx.SurveyTypeExportMappings.ToListAsync(cancellationToken: TestContext.Current.CancellationToken)).Should().ContainSingle().Which.TenantId.Should().Be(tenant2);
        }

        using (var ctx = CreateTestContext(connection, idGenerator, bypassTenant))
        {
            (await ctx.ExportFormats.CountAsync(cancellationToken: TestContext.Current.CancellationToken)).Should().Be(2);
            (await ctx.FormExportSchemas.CountAsync(cancellationToken: TestContext.Current.CancellationToken)).Should().Be(2);
            (await ctx.FlattenedSubmissions.CountAsync(cancellationToken: TestContext.Current.CancellationToken)).Should().Be(2);
            (await ctx.SurveyTypeExportMappings.CountAsync(cancellationToken: TestContext.Current.CancellationToken)).Should().Be(2);
        }
    }

    private sealed class IncrementingIdGenerator : IIdGenerator<long>
    {
        private long _current;
        public long CreateId() => Interlocked.Increment(ref _current);
    }

    private static TestReportingDbContext CreateTestContext(
        SqliteConnection connection,
        IIdGenerator<long> idGenerator,
        ITenantContext tenantContext)
    {
        var options = new DbContextOptionsBuilder<ReportingDbContext>()
            .UseSqlite(connection)
            .Options;

        return new TestReportingDbContext(options, idGenerator, tenantContext);
    }

    private sealed class TestReportingDbContext : ReportingDbContext
    {
        public TestReportingDbContext(
            DbContextOptions<ReportingDbContext> options,
            IIdGenerator<long> idGenerator,
            ITenantContext tenantContext)
            : base(options, idGenerator, tenantContext) { }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            builder.HasDefaultSchema("reporting");
            builder.ApplyEndatixQueryFilters(this);
            builder.ApplyConfigurationsFor<ReportingDbContext>(typeof(ReportingDbContext).Assembly);
        }
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
