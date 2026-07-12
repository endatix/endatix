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
public sealed class FormSchemaProviderIntegrationTests
{
    private const long TenantId = 1;
    private const long FormId = 100;
    private const long FormDefinitionId = 200;
    private const string DefinitionJson = """{"pages":[{"name":"p1","elements":[{"type":"text","name":"q1","title":"Question 1"}]}]}""";

    private readonly DbIntegrationFixture _fixture;

    public FormSchemaProviderIntegrationTests(DbIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task GetOrCompileAsync_WithMatchingDefinition_PersistsCompiledSchema()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await ResetReportingSchemaAsync(cancellationToken);

        IFormsRepository formsRepository = Substitute.For<IFormsRepository>();
        formsRepository
            .SingleOrDefaultAsync(Arg.Any<DefinitionByFormAndDefinitionIdSpec>(), cancellationToken)
            .Returns(CreateFormDefinition());

        await using ReportingDbContext dbContext = CreateContext(TenantId);
        FormSchemaRepository schemaRepository = CreateSchemaRepository(dbContext);
        FormSchemaProcessor schemaProcessor = new(
            formsRepository,
            schemaRepository,
            new FormSchemaCompiler(),
            NullLogger<FormSchemaProcessor>.Instance);
        FormSchemaProvider provider = new(schemaRepository, schemaProcessor);

        FormSchema? result = await provider.GetOrCompileAsync(
            TenantId,
            FormId,
            FormDefinitionId,
            cancellationToken);

        result.Should().NotBeNull();
        result!.FormDefinitionRevision.Should().Be(FormDefinitionId);
        result.SchemaJson.Should().Contain("q1");

        FormSchema? persisted = await schemaRepository.GetByFormIdAsync(TenantId, FormId, cancellationToken);
        persisted.Should().NotBeNull();
        persisted!.SchemaJson.Should().Be(result.SchemaJson);
    }

    [Fact]
    public async Task GetOrCompileAsync_WithCurrentSchema_ReturnsWithoutRecompiling()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await ResetReportingSchemaAsync(cancellationToken);

        IFormsRepository formsRepository = Substitute.For<IFormsRepository>();
        formsRepository
            .SingleOrDefaultAsync(Arg.Any<DefinitionByFormAndDefinitionIdSpec>(), cancellationToken)
            .Returns(CreateFormDefinition());

        await using ReportingDbContext dbContext = CreateContext(TenantId);
        FormSchemaRepository schemaRepository = CreateSchemaRepository(dbContext);
        FormSchemaProcessor schemaProcessor = new(
            formsRepository,
            schemaRepository,
            new FormSchemaCompiler(),
            NullLogger<FormSchemaProcessor>.Instance);
        FormSchemaProvider provider = new(schemaRepository, schemaProcessor);

        FormSchema? first = await provider.GetOrCompileAsync(TenantId, FormId, FormDefinitionId, cancellationToken);
        FormSchema? second = await provider.GetOrCompileAsync(TenantId, FormId, FormDefinitionId, cancellationToken);

        second.Should().BeSameAs(first);
        (await dbContext.FormSchemas.CountAsync(cancellationToken)).Should().Be(1);
        await formsRepository.Received(1).SingleOrDefaultAsync(
            Arg.Any<DefinitionByFormAndDefinitionIdSpec>(),
            cancellationToken);
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

        Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<ReportingDbContext> optionsBuilder =
            ReportingTestSchema.ConfigureOptionsBuilder(_fixture.ConnectionString);

        return new ReportingDbContext(optionsBuilder.Options, new IncrementingIdGenerator(), tenantContext);
    }

    private static FormSchemaRepository CreateSchemaRepository(ReportingDbContext dbContext)
    {
        ReportingUnitOfWork unitOfWork = new(dbContext);
        return new FormSchemaRepository(dbContext, unitOfWork);
    }

    private static FormDefinition CreateFormDefinition()
    {
        return new FormDefinition(TenantId, jsonData: DefinitionJson) { Id = FormDefinitionId };
    }
}
