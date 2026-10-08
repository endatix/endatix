using System.Text.Json.Nodes;
using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Entities;
using Endatix.Core.Specifications;
using Endatix.Infrastructure.Data;
using Endatix.Infrastructure.Features.Outbox;
using Endatix.IntegrationTests.Shared;
using Endatix.Modules.Reporting.Data;
using Endatix.Modules.Reporting.Domain;
using Endatix.Modules.Reporting.Features.FormSchema;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;
using Endatix.Modules.Reporting.Persistence;
using Endatix.Persistence.PostgreSql.Locking;
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

        IFormsRepository formsRepository = CreateFormsRepository();

        await using ReportingDbContext dbContext = CreateReportingContext(TenantId);
        await using AppDbContext appDbContext = CreateAppDbContext();
        FormSchemaRepository schemaRepository = CreateSchemaRepository(dbContext);
        FormSchemaProvider provider = CreateProvider(formsRepository, dbContext, appDbContext);

        FormSchema? result = await provider.GetOrCompileAsync(
            TenantId,
            FormId,
            FormDefinitionId,
            cancellationToken);

        result.Should().NotBeNull();
        result!.FormDefinitionRevision.Should().Be(FormDefinitionId);
        result.FlatteningMap.Should().Contain("q1");

        dbContext.ChangeTracker.Clear();
        FormSchema? persisted = await schemaRepository.GetByFormIdAsync(TenantId, FormId, cancellationToken);
        persisted.Should().NotBeNull();
        JsonNode.DeepEquals(JsonNode.Parse(persisted!.FlatteningMap), JsonNode.Parse(result.FlatteningMap))
            .Should()
            .BeTrue();
        JsonNode.DeepEquals(JsonNode.Parse(persisted.Codebook), JsonNode.Parse(result.Codebook))
            .Should()
            .BeTrue();
    }

    [Fact]
    public async Task GetOrCompileAsync_WithCurrentSchema_ReturnsWithoutRecompiling()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await ResetReportingSchemaAsync(cancellationToken);

        IFormsRepository formsRepository = CreateFormsRepository();

        await using ReportingDbContext dbContext = CreateReportingContext(TenantId);
        await using AppDbContext appDbContext = CreateAppDbContext();
        FormSchemaProvider provider = CreateProvider(formsRepository, dbContext, appDbContext);

        FormSchema? first = await provider.GetOrCompileAsync(TenantId, FormId, FormDefinitionId, cancellationToken);
        int definitionReadsToCompile = DefinitionReads(formsRepository);
        FormSchema? second = await provider.GetOrCompileAsync(TenantId, FormId, FormDefinitionId, cancellationToken);

        second.Should().BeSameAs(first);
        (await dbContext.FormSchemas.CountAsync(cancellationToken)).Should().Be(1);
        DefinitionReads(formsRepository).Should().Be(definitionReadsToCompile);
    }

    private static int DefinitionReads(IFormsRepository formsRepository) =>
        formsRepository.ReceivedCalls().Count(call =>
            call.GetArguments().FirstOrDefault() is DefinitionByFormAndDefinitionIdSpec);

    private async Task ResetReportingSchemaAsync(CancellationToken cancellationToken)
    {
        await _fixture.Checkpoint.ResetAsync(_fixture.ConnectionString, _fixture.Provider, cancellationToken);
        await ReportingTestSchema.EnsureMigratedAsync(_fixture.ConnectionString, _fixture.Provider, cancellationToken);
    }

    private ReportingDbContext CreateReportingContext(long tenantId)
    {
        IntegrationTenantContext tenantContext = new(tenantId);

        DbContextOptionsBuilder<ReportingDbContext> optionsBuilder =
            ReportingTestSchema.ConfigureOptionsBuilder(_fixture.ConnectionString);

        return new ReportingDbContext(optionsBuilder.Options, tenantContext);
    }

    private AppDbContext CreateAppDbContext()
    {
        IntegrationTenantContext tenantContext = new(TenantId);

        DbContextOptionsBuilder<AppDbContext> optionsBuilder = new();
        IntegrationAppDbContextFactory.ConfigurePostgreSqlOptions(optionsBuilder, _fixture.ConnectionString);

        return new AppDbContext(
            optionsBuilder.Options,
            tenantContext,
            new OutboxIntegrationEventDispatcher());
    }

    private static FormSchemaRepository CreateSchemaRepository(ReportingDbContext dbContext)
    {
        ReportingUnitOfWork unitOfWork = new(dbContext);
        return new FormSchemaRepository(dbContext, unitOfWork, new PostgreSqlTransactionLock());
    }

    private static FormSchemaProvider CreateProvider(
        IFormsRepository formsRepository,
        ReportingDbContext dbContext,
        AppDbContext appDbContext)
    {
        ReportingUnitOfWork unitOfWork = new(dbContext);
        FormSchemaRepository schemaRepository = new(dbContext, unitOfWork, new PostgreSqlTransactionLock());
        FormSchemaProcessor schemaProcessor = new(
            formsRepository,
            schemaRepository,
            Substitute.For<IFlattenedSubmissionRepository>(),
            unitOfWork,
            appDbContext,
            new FormSchemaCompiler(),
            NullLogger<FormSchemaProcessor>.Instance);

        return new FormSchemaProvider(schemaRepository, schemaProcessor, new FormSchemaCoverage(formsRepository, new FormSchemaCompiler()));
    }

    // The form is read as its definition and still exists after the compile wrote its schema.
    private static IFormsRepository CreateFormsRepository()
    {
        IFormsRepository formsRepository = Substitute.For<IFormsRepository>();
        formsRepository
            .SingleOrDefaultAsync(Arg.Any<DefinitionByFormAndDefinitionIdSpec>(), Arg.Any<CancellationToken>())
            .Returns(CreateFormDefinition());
        formsRepository
            .AnyAsync(Arg.Any<FormSpecifications.ById>(), Arg.Any<CancellationToken>())
            .Returns(true);
        return formsRepository;
    }

    private static FormDefinition CreateFormDefinition()
    {
        return new FormDefinition(TenantId, jsonData: DefinitionJson) { Id = FormDefinitionId };
    }
}
