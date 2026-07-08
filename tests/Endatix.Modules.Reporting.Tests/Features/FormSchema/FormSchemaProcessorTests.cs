using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Entities;
using Endatix.Modules.Reporting.Data;
using Endatix.Modules.Reporting.Domain;
using Endatix.Modules.Reporting.Features.FormSchema;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace Endatix.Modules.Reporting.Tests.Features.FormSchema;

public class FormSchemaProcessorTests
{
    private const long TenantId = 1;
    private const long FormId = 100;
    private const long FormDefinitionId = 200;

    [Fact]
    public async Task ProcessAsync_WithNoExistingSchema_CreatesAndPersistsSchema()
    {
        IFormsRepository formsRepository = Substitute.For<IFormsRepository>();
        IFormExportSchemaRepository schemaRepository = Substitute.For<IFormExportSchemaRepository>();
        Form form = CreateFormWithActiveDefinition("""{"pages":[{"name":"p1","elements":[{"type":"text","name":"q1"}]}]}""");
        formsRepository.SingleOrDefaultAsync(Arg.Any<Ardalis.Specification.ISingleResultSpecification<Form>>(), Arg.Any<CancellationToken>())
            .Returns(form);
        schemaRepository.GetByFormIdAsync(TenantId, FormId, Arg.Any<CancellationToken>())
            .Returns((FormExportSchema?)null);

        FormSchemaProcessor processor = new(
            formsRepository,
            schemaRepository,
            NullLogger<FormSchemaProcessor>.Instance);

        await processor.ProcessAsync(TenantId, FormId, FormDefinitionId, TestContext.Current.CancellationToken);

        await schemaRepository.Received(1).SaveAsync(
            Arg.Is<FormExportSchema>(schema =>
                schema.TenantId == TenantId &&
                schema.FormId == FormId &&
                schema.FormDefinitionRevision == FormDefinitionId &&
                schema.SchemaJson.Contains("q1")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessAsync_WithExistingSchema_UpdatesPersistedSchema()
    {
        IFormsRepository formsRepository = Substitute.For<IFormsRepository>();
        IFormExportSchemaRepository schemaRepository = Substitute.For<IFormExportSchemaRepository>();
        Form form = CreateFormWithActiveDefinition("""{"pages":[{"name":"p1","elements":[{"type":"text","name":"q2"}]}]}""");
        FormExportSchema existing = new(TenantId, FormId, FormDefinitionId, "[]");
        formsRepository.SingleOrDefaultAsync(Arg.Any<Ardalis.Specification.ISingleResultSpecification<Form>>(), Arg.Any<CancellationToken>())
            .Returns(form);
        schemaRepository.GetByFormIdAsync(TenantId, FormId, Arg.Any<CancellationToken>())
            .Returns(existing);

        FormSchemaProcessor processor = new(
            formsRepository,
            schemaRepository,
            NullLogger<FormSchemaProcessor>.Instance);

        await processor.ProcessAsync(TenantId, FormId, FormDefinitionId, TestContext.Current.CancellationToken);

        existing.SchemaJson.Should().Contain("q2");
        await schemaRepository.Received(1).SaveAsync(existing, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessAsync_WithStaleDefinition_DoesNotPersistSchema()
    {
        IFormsRepository formsRepository = Substitute.For<IFormsRepository>();
        IFormExportSchemaRepository schemaRepository = Substitute.For<IFormExportSchemaRepository>();
        Form form = CreateFormWithActiveDefinition("""{"pages":[]}""");
        formsRepository.SingleOrDefaultAsync(Arg.Any<Ardalis.Specification.ISingleResultSpecification<Form>>(), Arg.Any<CancellationToken>())
            .Returns(form);

        FormSchemaProcessor processor = new(
            formsRepository,
            schemaRepository,
            NullLogger<FormSchemaProcessor>.Instance);

        await processor.ProcessAsync(TenantId, FormId, formDefinitionId: 999, TestContext.Current.CancellationToken);

        await schemaRepository.DidNotReceive().GetByFormIdAsync(Arg.Any<long>(), Arg.Any<long>(), Arg.Any<CancellationToken>());
        await schemaRepository.DidNotReceive().SaveAsync(Arg.Any<FormExportSchema>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessAsync_WithTenantMismatch_ThrowsInvalidOperationException()
    {
        IFormsRepository formsRepository = Substitute.For<IFormsRepository>();
        IFormExportSchemaRepository schemaRepository = Substitute.For<IFormExportSchemaRepository>();
        Form form = new(2, "Other tenant form", isEnabled: true) { Id = FormId };
        FormDefinition definition = new(2, jsonData: """{"pages":[]}""") { Id = FormDefinitionId };
        form.AddFormDefinition(definition);
        formsRepository.SingleOrDefaultAsync(Arg.Any<Ardalis.Specification.ISingleResultSpecification<Form>>(), Arg.Any<CancellationToken>())
            .Returns(form);

        FormSchemaProcessor processor = new(
            formsRepository,
            schemaRepository,
            NullLogger<FormSchemaProcessor>.Instance);

        Func<Task> act = () => processor.ProcessAsync(TenantId, FormId, FormDefinitionId, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Tenant mismatch*");
        await schemaRepository.DidNotReceive().SaveAsync(Arg.Any<FormExportSchema>(), Arg.Any<CancellationToken>());
    }

    private static Form CreateFormWithActiveDefinition(string jsonData)
    {
        Form form = new(TenantId, "Test form", isEnabled: true) { Id = FormId };
        FormDefinition definition = new(TenantId, jsonData: jsonData) { Id = FormDefinitionId };
        form.AddFormDefinition(definition);
        return form;
    }
}
