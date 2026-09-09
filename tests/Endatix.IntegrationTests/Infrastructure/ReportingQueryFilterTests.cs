using System.Linq.Expressions;
using Endatix.Core.Abstractions;
using Endatix.IntegrationTests.Shared;
using Endatix.Modules.Reporting.Contracts.Export;
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
        await ReportingTestSchema.EnsureMigratedAsync(_fixture.ConnectionString, _fixture.Provider, cancellationToken);

        IncrementingIdGenerator idGenerator = new();
        TestTenantContext bypassTenant = TestTenantContext.Bypass;

        // High, unseeded ids: InitialReporting / SeedDefaultExportFormats seed export formats
        // for every row in "Tenants", so low ids collide with real tenants.
        const long tenant1 = 9201;
        const long tenant2 = 9202;

        using (var seedContext = CreateContext(idGenerator, bypassTenant))
        {
            seedContext.ExportFormats.AddRange(
                new ExportFormat(tenant1, "Tenant1 Export", ExportTarget.Submissions, ExportDeliveryFormat.Csv) { Id = 101 },
                new ExportFormat(tenant2, "Tenant2 Export", ExportTarget.Submissions, ExportDeliveryFormat.Json) { Id = 102 });

            seedContext.FormSchemas.AddRange(
                new FormSchema(tenant1, formId: 1, formDefinitionRevision: 1, flatteningMap: """{"version":1,"columns":[{"key":"a"}]}""", codebook: FormSchema.EmptyCodebookJson) { Id = 201 },
                new FormSchema(tenant2, formId: 2, formDefinitionRevision: 1, flatteningMap: """{"version":1,"columns":[{"key":"b"}]}""", codebook: FormSchema.EmptyCodebookJson) { Id = 202 });

            seedContext.FlattenedSubmissions.AddRange(
                new FlattenedSubmission(submissionId: 301, tenantId: tenant1, formId: 1),
                new FlattenedSubmission(submissionId: 302, tenantId: tenant2, formId: 2));

            seedContext.SurveyTypeExportMappings.AddRange(
                new SurveyTypeExportMapping(tenant1, exportFormatId: 101) { Id = 401 },
                new SurveyTypeExportMapping(tenant2, exportFormatId: 102) { Id = 402 });

            await seedContext.SaveChangesAsync(cancellationToken);
        }

        // Act & Assert — Tenant 1 isolation
        TestTenantContext tenant1Ctx = new(tenant1);
        using (var ctx = CreateContext(idGenerator, tenant1Ctx))
        {
            (await ctx.ExportFormats.ToListAsync(cancellationToken)).Should().ContainSingle().Which.TenantId.Should().Be(tenant1);
            (await ctx.FormSchemas.ToListAsync(cancellationToken)).Should().ContainSingle().Which.TenantId.Should().Be(tenant1);
            (await ctx.FlattenedSubmissions.ToListAsync(cancellationToken)).Should().ContainSingle().Which.TenantId.Should().Be(tenant1);
            (await ctx.SurveyTypeExportMappings.ToListAsync(cancellationToken)).Should().ContainSingle().Which.TenantId.Should().Be(tenant1);
        }

        // Act & Assert — Tenant 2 isolation
        TestTenantContext tenant2Ctx = new(tenant2);
        using (var ctx = CreateContext(idGenerator, tenant2Ctx))
        {
            (await ctx.ExportFormats.ToListAsync(cancellationToken)).Should().ContainSingle().Which.TenantId.Should().Be(tenant2);
            (await ctx.FormSchemas.ToListAsync(cancellationToken)).Should().ContainSingle().Which.TenantId.Should().Be(tenant2);
            (await ctx.FlattenedSubmissions.ToListAsync(cancellationToken)).Should().ContainSingle().Which.TenantId.Should().Be(tenant2);
            (await ctx.SurveyTypeExportMappings.ToListAsync(cancellationToken)).Should().ContainSingle().Which.TenantId.Should().Be(tenant2);
        }

        // Act & Assert — Bypass (no isolation)
        using (var ctx = CreateContext(idGenerator, bypassTenant))
        {
            (await ctx.ExportFormats.CountAsync(InTestTenants<ExportFormat>(), cancellationToken)).Should().Be(2);
            (await ctx.FormSchemas.CountAsync(InTestTenants<FormSchema>(), cancellationToken)).Should().Be(2);
            (await ctx.FlattenedSubmissions.CountAsync(InTestTenants<FlattenedSubmission>(), cancellationToken)).Should().Be(2);
            (await ctx.SurveyTypeExportMappings.CountAsync(InTestTenants<SurveyTypeExportMapping>(), cancellationToken)).Should().Be(2);

            static Expression<Func<T, bool>> InTestTenants<T>() where T : ITenantOwned =>
                row => row.TenantId == tenant1 || row.TenantId == tenant2;
        }
    }

    private TestReportingDbContext CreateContext(IIdGenerator<long> idGenerator, ITenantContext tenantContext)
    {
        var optionsBuilder =
            ReportingTestSchema.ConfigureOptionsBuilder(_fixture.ConnectionString);

        return new TestReportingDbContext(optionsBuilder.Options, idGenerator, tenantContext);
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
