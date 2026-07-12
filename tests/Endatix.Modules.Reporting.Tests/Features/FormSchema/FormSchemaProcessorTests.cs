using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Entities;
using Endatix.Modules.Reporting.Data;
using Endatix.Modules.Reporting.Domain;
using Endatix.Modules.Reporting.Features.FormSchema;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;
using FluentAssertions;
using FormSchemaEntity = Endatix.Modules.Reporting.Domain.FormSchema;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Endatix.Modules.Reporting.Tests.Features.FormSchema;

public class FormSchemaProcessorTests
{
    private const long TenantId = 1;
    private const long FormId = 100;
    private const long FormDefinitionId = 200;
    private const long HistoricalFormDefinitionId = 100;

    [Fact]
    public async Task FormSchemaProcessor_ProcessAsync_WithNoExistingSchema_CreatesAndPersistsSchema()
    {
        IFormsRepository formsRepository = Substitute.For<IFormsRepository>();
        IFormSchemaRepository schemaRepository = Substitute.For<IFormSchemaRepository>();
        FormDefinition definition = CreateFormDefinition(
            FormDefinitionId,
            """{"pages":[{"name":"p1","elements":[{"type":"text","name":"q1"}]}]}""");
        formsRepository.SingleOrDefaultAsync(Arg.Any<DefinitionByFormAndDefinitionIdSpec>(), Arg.Any<CancellationToken>())
            .Returns(definition);
        schemaRepository.GetByFormIdAsync(TenantId, FormId, Arg.Any<CancellationToken>())
            .Returns((FormSchemaEntity?)null);

        FormSchemaProcessor processor = new(
            formsRepository,
            schemaRepository,
            new FormSchemaCompiler(),
            NullLogger<FormSchemaProcessor>.Instance);

        await processor.ProcessAsync(TenantId, FormId, FormDefinitionId, TestContext.Current.CancellationToken);

        await schemaRepository.Received(1).SaveAsync(
            Arg.Is<FormSchemaEntity>(schema =>
                schema.TenantId == TenantId &&
                schema.FormId == FormId &&
                schema.FormDefinitionRevision == FormDefinitionId &&
                schema.FlatteningMap.Contains("q1") &&
                schema.Codebook.Contains("version")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FormSchemaProcessor_ProcessAsync_WithExistingSchema_UpdatesPersistedSchema()
    {
        IFormsRepository formsRepository = Substitute.For<IFormsRepository>();
        IFormSchemaRepository schemaRepository = Substitute.For<IFormSchemaRepository>();
        FormDefinition definition = CreateFormDefinition(
            FormDefinitionId,
            """{"pages":[{"name":"p1","elements":[{"type":"text","name":"q2"}]}]}""");
        FormSchemaEntity existing = new(
            TenantId,
            FormId,
            FormDefinitionId,
            FormSchemaEntity.EmptyFlatteningMapJson,
            FormSchemaEntity.EmptyCodebookJson);
        formsRepository.SingleOrDefaultAsync(Arg.Any<DefinitionByFormAndDefinitionIdSpec>(), Arg.Any<CancellationToken>())
            .Returns(definition);
        schemaRepository.GetByFormIdAsync(TenantId, FormId, Arg.Any<CancellationToken>())
            .Returns(existing);

        FormSchemaProcessor processor = new(
            formsRepository,
            schemaRepository,
            new FormSchemaCompiler(),
            NullLogger<FormSchemaProcessor>.Instance);

        await processor.ProcessAsync(TenantId, FormId, FormDefinitionId, TestContext.Current.CancellationToken);

        existing.FlatteningMap.Should().Contain("q2");
        existing.Codebook.Should().Contain("version");
        await schemaRepository.Received(1).SaveAsync(existing, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FormSchemaProcessor_ProcessAsync_WithHistoricalDefinition_MergesIntoExistingSchemaWithoutRollingBackRevision()
    {
        IFormsRepository formsRepository = Substitute.For<IFormsRepository>();
        IFormSchemaRepository schemaRepository = Substitute.For<IFormSchemaRepository>();
        FormDefinition historicalDefinition = CreateFormDefinition(
            HistoricalFormDefinitionId,
            """{"pages":[{"name":"p1","elements":[{"type":"text","name":"q1"}]}]}""");
        FormSchemaCompiler compiler = new();
        FormSchemaCompileResult existingCompiled = compiler.CompilePersisted(
            """{"pages":[{"name":"p1","elements":[{"type":"text","name":"q2","title":"Question 2"}]}]}""");
        FormSchemaEntity existing = new(
            TenantId,
            FormId,
            FormDefinitionId,
            existingCompiled.FlatteningMapJson,
            existingCompiled.CodebookJson);
        formsRepository.SingleOrDefaultAsync(Arg.Any<DefinitionByFormAndDefinitionIdSpec>(), Arg.Any<CancellationToken>())
            .Returns(historicalDefinition);
        schemaRepository.GetByFormIdAsync(TenantId, FormId, Arg.Any<CancellationToken>())
            .Returns(existing);

        FormSchemaProcessor processor = new(
            formsRepository,
            schemaRepository,
            compiler,
            NullLogger<FormSchemaProcessor>.Instance);

        await processor.ProcessAsync(TenantId, FormId, HistoricalFormDefinitionId, TestContext.Current.CancellationToken);

        existing.FormDefinitionRevision.Should().Be(FormDefinitionId);
        existing.FlatteningMap.Should().Contain("q1");
        existing.FlatteningMap.Should().Contain("q2");
        await schemaRepository.Received(1).SaveAsync(existing, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FormSchemaProcessor_ProcessAsync_WithMissingDefinition_DoesNotPersistSchema()
    {
        IFormsRepository formsRepository = Substitute.For<IFormsRepository>();
        IFormSchemaRepository schemaRepository = Substitute.For<IFormSchemaRepository>();
        formsRepository.SingleOrDefaultAsync(Arg.Any<DefinitionByFormAndDefinitionIdSpec>(), Arg.Any<CancellationToken>())
            .Returns((FormDefinition?)null);

        FormSchemaProcessor processor = new(
            formsRepository,
            schemaRepository,
            new FormSchemaCompiler(),
            NullLogger<FormSchemaProcessor>.Instance);

        await processor.ProcessAsync(TenantId, FormId, formDefinitionId: 999, TestContext.Current.CancellationToken);

        await schemaRepository.DidNotReceive().GetByFormIdAsync(Arg.Any<long>(), Arg.Any<long>(), Arg.Any<CancellationToken>());
        await schemaRepository.DidNotReceive().SaveAsync(Arg.Any<FormSchemaEntity>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FormSchemaProcessor_ProcessAsync_WithTenantMismatch_ThrowsInvalidOperationException()
    {
        IFormsRepository formsRepository = Substitute.For<IFormsRepository>();
        IFormSchemaRepository schemaRepository = Substitute.For<IFormSchemaRepository>();
        FormDefinition definition = CreateFormDefinition(
            FormDefinitionId,
            """{"pages":[]}""",
            tenantId: 2);
        formsRepository.SingleOrDefaultAsync(Arg.Any<DefinitionByFormAndDefinitionIdSpec>(), Arg.Any<CancellationToken>())
            .Returns(definition);

        FormSchemaProcessor processor = new(
            formsRepository,
            schemaRepository,
            new FormSchemaCompiler(),
            NullLogger<FormSchemaProcessor>.Instance);

        Func<Task> act = () => processor.ProcessAsync(TenantId, FormId, FormDefinitionId, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Tenant mismatch*");
        await schemaRepository.DidNotReceive().SaveAsync(Arg.Any<FormSchemaEntity>(), Arg.Any<CancellationToken>());
    }

    private static FormDefinition CreateFormDefinition(long id, string jsonData, long tenantId = TenantId) =>
        new(tenantId, jsonData: jsonData) { Id = id };
}
