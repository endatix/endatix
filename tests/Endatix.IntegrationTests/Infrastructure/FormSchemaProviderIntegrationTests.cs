using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Entities;
using Endatix.IntegrationTests.Shared;
using Endatix.Modules.Reporting.Data;
using Endatix.Modules.Reporting.Domain;
using Endatix.Modules.Reporting.Features.FormSchema;
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
            .SingleOrDefaultAsync(Arg.Any<Ardalis.Specification.ISingleResultSpecification<Form>>(), cancellationToken)
            .Returns(CreateFormWithActiveDefinition());

        await using ReportingDbContext dbContext = CreateContext(TenantId);
        FormExportSchemaRepository schemaRepository = CreateSchemaRepository(dbContext);
        FormSchemaProcessor schemaProcessor = new(
            formsRepository,
            schemaRepository,
            NullLogger<FormSchemaProcessor>.Instance);
        FormSchemaProvider provider = new(schemaRepository, schemaProcessor);

        FormExportSchema? result = await provider.GetOrCompileAsync(
            TenantId,
            FormId,
            FormDefinitionId,
            cancellationToken);

        result.Should().NotBeNull();
        result!.FormDefinitionRevision.Should().Be(FormDefinitionId);
        result.SchemaJson.Should().Contain("q1");

        FormExportSchema? persisted = await schemaRepository.GetByFormIdAsync(TenantId, FormId, cancellationToken);
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
            .SingleOrDefaultAsync(Arg.Any<Ardalis.Specification.ISingleResultSpecification<Form>>(), cancellationToken)
            .Returns(CreateFormWithActiveDefinition());

        await using ReportingDbContext dbContext = CreateContext(TenantId);
        FormExportSchemaRepository schemaRepository = CreateSchemaRepository(dbContext);
        FormSchemaProcessor schemaProcessor = new(
            formsRepository,
            schemaRepository,
            NullLogger<FormSchemaProcessor>.Instance);
        FormSchemaProvider provider = new(schemaRepository, schemaProcessor);

        FormExportSchema? first = await provider.GetOrCompileAsync(TenantId, FormId, FormDefinitionId, cancellationToken);
        FormExportSchema? second = await provider.GetOrCompileAsync(TenantId, FormId, FormDefinitionId, cancellationToken);

        second.Should().BeSameAs(first);
        (await dbContext.FormExportSchemas.CountAsync(cancellationToken)).Should().Be(1);
        await formsRepository.Received(1).SingleOrDefaultAsync(
            Arg.Any<Ardalis.Specification.ISingleResultSpecification<Form>>(),
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

    private static FormExportSchemaRepository CreateSchemaRepository(ReportingDbContext dbContext)
    {
        ReportingUnitOfWork unitOfWork = new(dbContext);
        return new FormExportSchemaRepository(dbContext, unitOfWork);
    }

    private static Form CreateFormWithActiveDefinition()
    {
        Form form = new(TenantId, "Integration form", isEnabled: true) { Id = FormId };
        FormDefinition definition = new(TenantId, jsonData: DefinitionJson) { Id = FormDefinitionId };
        form.AddFormDefinition(definition);
        return form;
    }

    private sealed class IncrementingIdGenerator : IIdGenerator<long>
    {
        private long _current;

        public long CreateId() => Interlocked.Increment(ref _current);
    }
}
