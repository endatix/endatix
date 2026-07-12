using Endatix.Modules.Reporting.Data;
using Endatix.Modules.Reporting.Features.FormSchema;
using FluentAssertions;
using FormSchemaEntity = Endatix.Modules.Reporting.Domain.FormSchema;
using NSubstitute;

namespace Endatix.Modules.Reporting.Tests.Features.FormSchema;

public class FormSchemaProviderTests
{
    private const long TenantId = 1;
    private const long FormId = 100;
    private const long FormDefinitionId = 200;
    private const long HistoricalFormDefinitionId = 100;

    [Fact]
    public async Task FormSchemaProvider_GetOrCompileAsync_WithCurrentSchema_ReturnsWithoutInvokingProcessor()
    {
        FormSchemaEntity schema = new(TenantId, FormId, FormDefinitionId, """{"columns":[]}""");
        IFormSchemaRepository schemaRepository = Substitute.For<IFormSchemaRepository>();
        schemaRepository.GetByFormIdAsync(TenantId, FormId, Arg.Any<CancellationToken>())
            .Returns(schema);
        IFormSchemaProcessor schemaProcessor = Substitute.For<IFormSchemaProcessor>();

        FormSchemaProvider provider = new(schemaRepository, schemaProcessor);

        FormSchemaEntity? result = await provider.GetOrCompileAsync(
            TenantId,
            FormId,
            FormDefinitionId,
            TestContext.Current.CancellationToken);

        result.Should().BeSameAs(schema);
        await schemaProcessor.DidNotReceive().ProcessAsync(
            Arg.Any<long>(),
            Arg.Any<long>(),
            Arg.Any<long>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FormSchemaProvider_GetOrCompileAsync_WithStaleSchema_InvokesProcessorAndReturnsRefreshedSchema()
    {
        FormSchemaEntity refreshed = new(TenantId, FormId, FormDefinitionId, """{"columns":[{"key":"q1"}]}""");
        IFormSchemaRepository schemaRepository = Substitute.For<IFormSchemaRepository>();
        schemaRepository.GetByFormIdAsync(TenantId, FormId, Arg.Any<CancellationToken>())
            .Returns(
                new FormSchemaEntity(TenantId, FormId, formDefinitionRevision: 1, "[]"),
                refreshed);
        IFormSchemaProcessor schemaProcessor = Substitute.For<IFormSchemaProcessor>();

        FormSchemaProvider provider = new(schemaRepository, schemaProcessor);

        FormSchemaEntity? result = await provider.GetOrCompileAsync(
            TenantId,
            FormId,
            FormDefinitionId,
            TestContext.Current.CancellationToken);

        result.Should().BeSameAs(refreshed);
        await schemaProcessor.Received(1).ProcessAsync(TenantId, FormId, FormDefinitionId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FormSchemaProvider_GetOrCompileAsync_WithHistoricalDefinition_ReturnsMergedSchema()
    {
        FormSchemaEntity merged = new(
            TenantId,
            FormId,
            FormDefinitionId,
            """[{"key":"q1"},{"key":"q2"}]""");
        IFormSchemaRepository schemaRepository = Substitute.For<IFormSchemaRepository>();
        schemaRepository.GetByFormIdAsync(TenantId, FormId, Arg.Any<CancellationToken>())
            .Returns(
                new FormSchemaEntity(TenantId, FormId, FormDefinitionId, """[{"key":"q2"}]"""),
                merged);
        IFormSchemaProcessor schemaProcessor = Substitute.For<IFormSchemaProcessor>();

        FormSchemaProvider provider = new(schemaRepository, schemaProcessor);

        FormSchemaEntity? result = await provider.GetOrCompileAsync(
            TenantId,
            FormId,
            HistoricalFormDefinitionId,
            TestContext.Current.CancellationToken);

        result.Should().BeSameAs(merged);
        await schemaProcessor.Received(1).ProcessAsync(
            TenantId,
            FormId,
            HistoricalFormDefinitionId,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FormSchemaProvider_GetOrCompileAsync_WhenProcessorDoesNotCoverRequestedDefinition_ReturnsNull()
    {
        IFormSchemaRepository schemaRepository = Substitute.For<IFormSchemaRepository>();
        schemaRepository.GetByFormIdAsync(TenantId, FormId, Arg.Any<CancellationToken>())
            .Returns(
                (FormSchemaEntity?)null,
                new FormSchemaEntity(TenantId, FormId, formDefinitionRevision: 1, "[]"));
        IFormSchemaProcessor schemaProcessor = Substitute.For<IFormSchemaProcessor>();

        FormSchemaProvider provider = new(schemaRepository, schemaProcessor);

        FormSchemaEntity? result = await provider.GetOrCompileAsync(
            TenantId,
            FormId,
            FormDefinitionId,
            TestContext.Current.CancellationToken);

        result.Should().BeNull();
        await schemaProcessor.Received(1).ProcessAsync(TenantId, FormId, FormDefinitionId, Arg.Any<CancellationToken>());
    }
}
