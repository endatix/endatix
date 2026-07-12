using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Entities;
using Endatix.IntegrationTests.Shared;
using Endatix.Modules.Reporting.Data;
using Endatix.Modules.Reporting.Domain;
using Endatix.Modules.Reporting.Features.FormSchema;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;
using Endatix.Modules.Reporting.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Endatix.IntegrationTests;

[Collection(nameof(DbIntegrationTestCollection))]
[Trait("Category", "Infrastructure")]
[Trait("Priority", "P1")]
[Trait("DbSpecific", "PostgreSql")]
public sealed class FormSchemaRepositoryTests
{
    private const long TenantId = 1;
    private const long FormId = 100;
    private const long FormDefinitionRevision = 200;
    private const string InitialFlatteningMapJson = FormSchema.EmptyFlatteningMapJson;
    private const string UpdatedFlatteningMapJson = """{"version":1,"columns":[{"key":"q1"}]}""";
    private const string CodebookJson = FormSchema.EmptyCodebookJson;

    private readonly DbIntegrationFixture _fixture;

    public FormSchemaRepositoryTests(DbIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task GetByFormIdAsync_WhenSchemaMissing_ReturnsNull()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await ResetReportingSchemaAsync(cancellationToken);

        await using ReportingDbContext dbContext = CreateContext(TenantId);
        FormSchemaRepository repository = CreateRepository(dbContext);

        FormSchema? result = await repository.GetByFormIdAsync(TenantId, FormId, cancellationToken);

        result.Should().BeNull();
    }

    [Fact]
    public async Task SaveAsync_WithNewSchema_PersistsToDatabase()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await ResetReportingSchemaAsync(cancellationToken);

        await using ReportingDbContext dbContext = CreateContext(TenantId);
        FormSchemaRepository repository = CreateRepository(dbContext);
        FormSchema schema = new(TenantId, FormId, FormDefinitionRevision, InitialFlatteningMapJson, CodebookJson);

        await repository.SaveAsync(schema, cancellationToken);

        FormSchema? persisted = await repository.GetByFormIdAsync(TenantId, FormId, cancellationToken);
        persisted.Should().NotBeNull();
        persisted!.FormDefinitionRevision.Should().Be(FormDefinitionRevision);
        persisted.FlatteningMap.Should().Be(InitialFlatteningMapJson);
        persisted.Codebook.Should().Be(CodebookJson);
        persisted.Id.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task SaveAsync_WithUpdatedSchema_PersistsToDatabase()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await ResetReportingSchemaAsync(cancellationToken);

        await using ReportingDbContext dbContext = CreateContext(TenantId);
        FormSchemaRepository repository = CreateRepository(dbContext);
        FormSchema schema = new(TenantId, FormId, FormDefinitionRevision, InitialFlatteningMapJson, CodebookJson);
        await repository.SaveAsync(schema, cancellationToken);

        schema.UpdateSchema(FormDefinitionRevision + 1, UpdatedFlatteningMapJson, CodebookJson);
        await repository.SaveAsync(schema, cancellationToken);

        FormSchema? persisted = await repository.GetByFormIdAsync(TenantId, FormId, cancellationToken);
        persisted.Should().NotBeNull();
        persisted!.FormDefinitionRevision.Should().Be(FormDefinitionRevision + 1);
        persisted.FlatteningMap.Should().Be(UpdatedFlatteningMapJson);
        var schemasCount = await dbContext.FormSchemas.CountAsync(cancellationToken);
        schemasCount.Should().Be(1);
    }

    [Fact]
    public async Task GetByFormIdAsync_WithOtherTenant_ReturnsNull()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await ResetReportingSchemaAsync(cancellationToken);

        await using ReportingDbContext dbContext = CreateContext(TenantId);
        FormSchemaRepository repository = CreateRepository(dbContext);
        FormSchema schema = new(TenantId, FormId, FormDefinitionRevision, InitialFlatteningMapJson, CodebookJson);
        await repository.SaveAsync(schema, cancellationToken);

        FormSchema? otherTenantResult = await repository.GetByFormIdAsync(
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

    private static FormSchemaRepository CreateRepository(ReportingDbContext dbContext)
    {
        ReportingUnitOfWork unitOfWork = new(dbContext);
        return new FormSchemaRepository(dbContext, unitOfWork);
    }
}
