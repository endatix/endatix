using Endatix.Core.Abstractions;
using Endatix.Infrastructure.Data;
using Endatix.IntegrationTests.Shared;
using Endatix.Modules.Reporting.Domain;
using Endatix.Modules.Reporting.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Endatix.IntegrationTests;

[Collection(nameof(DbIntegrationTestCollection))]
[Trait("Category", "Infrastructure")]
[Trait("Priority", "P1")]
[Trait("DbSpecific", "PostgreSql")]
public sealed class ReportingQueryFilterTests
{
    private readonly DbIntegrationFixture _fixture;

    public ReportingQueryFilterTests(DbIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task QueryFilters_EnforceTenantIsolation_ForAllReportingEntities()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await _fixture.Checkpoint.ResetAsync(_fixture.ConnectionString, _fixture.Provider, cancellationToken);
        await ReportingTestSchema.EnsureMigratedAsync(_fixture.ConnectionString, cancellationToken);

        IncrementingIdGenerator idGenerator = new();
        var bypassTenant = Substitute.For<ITenantContext>();
        bypassTenant.TenantId.Returns(0L);

        const long tenant1 = 1;
        const long tenant2 = 2;

        using (var seedContext = CreateContext(idGenerator, bypassTenant))
        {
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

            await seedContext.SaveChangesAsync(cancellationToken);
        }

        // Act & Assert — Tenant 1 isolation
        var tenant1Ctx = Substitute.For<ITenantContext>();
        tenant1Ctx.TenantId.Returns(tenant1);
        using (var ctx = CreateContext(idGenerator, tenant1Ctx))
        {
            (await ctx.ExportFormats.ToListAsync(cancellationToken)).Should().ContainSingle().Which.TenantId.Should().Be(tenant1);
            (await ctx.FormExportSchemas.ToListAsync(cancellationToken)).Should().ContainSingle().Which.TenantId.Should().Be(tenant1);
            (await ctx.FlattenedSubmissions.ToListAsync(cancellationToken)).Should().ContainSingle().Which.TenantId.Should().Be(tenant1);
            (await ctx.SurveyTypeExportMappings.ToListAsync(cancellationToken)).Should().ContainSingle().Which.TenantId.Should().Be(tenant1);
        }

        // Act & Assert — Tenant 2 isolation
        var tenant2Ctx = Substitute.For<ITenantContext>();
        tenant2Ctx.TenantId.Returns(tenant2);
        using (var ctx = CreateContext(idGenerator, tenant2Ctx))
        {
            (await ctx.ExportFormats.ToListAsync(cancellationToken)).Should().ContainSingle().Which.TenantId.Should().Be(tenant2);
            (await ctx.FormExportSchemas.ToListAsync(cancellationToken)).Should().ContainSingle().Which.TenantId.Should().Be(tenant2);
            (await ctx.FlattenedSubmissions.ToListAsync(cancellationToken)).Should().ContainSingle().Which.TenantId.Should().Be(tenant2);
            (await ctx.SurveyTypeExportMappings.ToListAsync(cancellationToken)).Should().ContainSingle().Which.TenantId.Should().Be(tenant2);
        }

        // Act & Assert — Bypass (no isolation)
        using (var ctx = CreateContext(idGenerator, bypassTenant))
        {
            (await ctx.ExportFormats.CountAsync(cancellationToken)).Should().Be(2);
            (await ctx.FormExportSchemas.CountAsync(cancellationToken)).Should().Be(2);
            (await ctx.FlattenedSubmissions.CountAsync(cancellationToken)).Should().Be(2);
            (await ctx.SurveyTypeExportMappings.CountAsync(cancellationToken)).Should().Be(2);
        }
    }

    private TestReportingDbContext CreateContext(IIdGenerator<long> idGenerator, ITenantContext tenantContext)
    {
        DbContextOptionsBuilder<ReportingDbContext> optionsBuilder = new();
        optionsBuilder.UseNpgsql(_fixture.ConnectionString);
        ModuleDbContextExtensions.ConfigureProviderScopedMigrations(
            optionsBuilder,
            ReportingPersistence.PostgreSqlMigrationsNamespace);

        return new TestReportingDbContext(optionsBuilder.Options, idGenerator, tenantContext);
    }

    private sealed class IncrementingIdGenerator : IIdGenerator<long>
    {
        private long _current;
        public long CreateId() => Interlocked.Increment(ref _current);
    }

    private sealed class TestReportingDbContext : ReportingDbContext
    {
        public TestReportingDbContext(
            DbContextOptions<ReportingDbContext> options,
            IIdGenerator<long> idGenerator,
            ITenantContext tenantContext)
            : base(options, idGenerator, tenantContext) { }
    }
}
