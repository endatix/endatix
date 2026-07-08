using Endatix.Core.Abstractions;
using Endatix.IntegrationTests.Shared;
using Endatix.Modules.Reporting.Data;
using Endatix.Modules.Reporting.Domain;
using Endatix.Modules.Reporting.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Endatix.IntegrationTests;

[Collection(nameof(DbIntegrationTestCollection))]
[Trait("Category", "Infrastructure")]
[Trait("Priority", "P1")]
[Trait("DbSpecific", "PostgreSql")]
public sealed class FormExportSchemaRepositoryTests
{
    private const long TenantId = 1;
    private const long FormId = 100;
    private const long FormDefinitionRevision = 200;
    private const string InitialSchemaJson = """{"columns":[]}""";
    private const string UpdatedSchemaJson = """{"columns":[{"key":"q1"}]}""";

    private readonly DbIntegrationFixture _fixture;

    public FormExportSchemaRepositoryTests(DbIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task GetByFormIdAsync_WhenSchemaMissing_ReturnsNull()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await ResetReportingSchemaAsync(cancellationToken);

        await using ReportingDbContext dbContext = CreateContext(TenantId);
        FormExportSchemaRepository repository = CreateRepository(dbContext);

        FormExportSchema? result = await repository.GetByFormIdAsync(TenantId, FormId, cancellationToken);

        result.Should().BeNull();
    }

    [Fact]
    public async Task SaveAsync_WithNewSchema_PersistsToDatabase()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await ResetReportingSchemaAsync(cancellationToken);

        await using ReportingDbContext dbContext = CreateContext(TenantId);
        FormExportSchemaRepository repository = CreateRepository(dbContext);
        FormExportSchema schema = new(TenantId, FormId, FormDefinitionRevision, InitialSchemaJson);

        await repository.SaveAsync(schema, cancellationToken);

        FormExportSchema? persisted = await repository.GetByFormIdAsync(TenantId, FormId, cancellationToken);
        persisted.Should().NotBeNull();
        persisted!.FormDefinitionRevision.Should().Be(FormDefinitionRevision);
        persisted.SchemaJson.Should().Be(InitialSchemaJson);
        persisted.Id.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task SaveAsync_WithUpdatedSchema_PersistsToDatabase()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await ResetReportingSchemaAsync(cancellationToken);

        await using ReportingDbContext dbContext = CreateContext(TenantId);
        FormExportSchemaRepository repository = CreateRepository(dbContext);
        FormExportSchema schema = new(TenantId, FormId, FormDefinitionRevision, InitialSchemaJson);
        await repository.SaveAsync(schema, cancellationToken);

        schema.UpdateSchema(FormDefinitionRevision + 1, UpdatedSchemaJson);
        await repository.SaveAsync(schema, cancellationToken);

        FormExportSchema? persisted = await repository.GetByFormIdAsync(TenantId, FormId, cancellationToken);
        persisted.Should().NotBeNull();
        persisted!.FormDefinitionRevision.Should().Be(FormDefinitionRevision + 1);
        persisted.SchemaJson.Should().Be(UpdatedSchemaJson);
        var schemasCount = await dbContext.FormExportSchemas.CountAsync(cancellationToken);
        schemasCount.Should().Be(1);
    }

    [Fact]
    public async Task GetByFormIdAsync_WithOtherTenant_ReturnsNull()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await ResetReportingSchemaAsync(cancellationToken);

        await using ReportingDbContext dbContext = CreateContext(TenantId);
        FormExportSchemaRepository repository = CreateRepository(dbContext);
        FormExportSchema schema = new(TenantId, FormId, FormDefinitionRevision, InitialSchemaJson);
        await repository.SaveAsync(schema, cancellationToken);

        FormExportSchema? otherTenantResult = await repository.GetByFormIdAsync(
            tenantId: 2,
            FormId,
            cancellationToken);

        otherTenantResult.Should().BeNull();
    }

    private async Task ResetReportingSchemaAsync(CancellationToken cancellationToken)
    {
        await _fixture.Checkpoint.ResetAsync(_fixture.ConnectionString, _fixture.Provider, cancellationToken);
        await ReportingTestSchema.EnsureMigratedAsync(_fixture.ConnectionString, cancellationToken);
    }

    private ReportingDbContext CreateContext(long tenantId)
    {
        ITenantContext tenantContext = Substitute.For<ITenantContext>();
        tenantContext.TenantId.Returns(tenantId);

        DbContextOptionsBuilder<ReportingDbContext> optionsBuilder =
            ReportingTestSchema.ConfigureOptionsBuilder(_fixture.ConnectionString);

        return new ReportingDbContext(optionsBuilder.Options, new IncrementingIdGenerator(), tenantContext);
    }

    private static FormExportSchemaRepository CreateRepository(ReportingDbContext dbContext)
    {
        ReportingUnitOfWork unitOfWork = new(dbContext);
        return new FormExportSchemaRepository(dbContext, unitOfWork);
    }
}
