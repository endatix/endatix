using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Entities;
using Endatix.Modules.Reporting.Data;
using Endatix.Modules.Reporting.Features.FormSchema;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;
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
    private const string HistoricalDefinitionJson = """{"pages":[{"elements":[{"type":"text","name":"q1"}]}]}""";
    private const string CurrentDefinitionJson = """{"pages":[{"elements":[{"type":"text","name":"q2"}]}]}""";

    private readonly IFormSchemaRepository _schemaRepository = Substitute.For<IFormSchemaRepository>();
    private readonly IFormSchemaProcessor _schemaProcessor = Substitute.For<IFormSchemaProcessor>();
    private readonly IFormsRepository _formsRepository = Substitute.For<IFormsRepository>();
    private readonly FormSchemaCompiler _compiler = new();

    [Fact]
    public async Task FormSchemaProvider_GetOrCompileAsync_WithCurrentSchema_ReturnsWithoutInvokingProcessor()
    {
        // Arrange
        var schema = Schema(FormDefinitionId, FormSchemaEntity.EmptyFlatteningMapJson);
        _schemaRepository.GetByFormIdAsync(TenantId, FormId, Arg.Any<CancellationToken>())
            .Returns(schema);

        // Act
        var result = await GetOrCompileAsync(FormDefinitionId);

        // Assert
        result.Should().BeSameAs(schema);
        await _schemaProcessor.DidNotReceiveWithAnyArgs().ProcessAsync(default, default, default, default, default);
    }

    [Fact]
    public async Task FormSchemaProvider_GetOrCompileAsync_WithStaleSchema_InvokesProcessorAndReturnsRefreshedSchema()
    {
        // Arrange
        var refreshed = Schema(FormDefinitionId, CompiledMap(CurrentDefinitionJson));
        _schemaRepository.GetByFormIdAsync(TenantId, FormId, Arg.Any<CancellationToken>())
            .Returns(Schema(1, FormSchemaEntity.EmptyFlatteningMapJson), refreshed);

        // Act
        var result = await GetOrCompileAsync(FormDefinitionId);

        // Assert
        result.Should().BeSameAs(refreshed);
        await _schemaProcessor.Received(1).ProcessAsync(
            TenantId,
            FormId,
            FormDefinitionId,
            cancellationToken: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FormSchemaProvider_GetOrCompileAsync_WithHistoricalDefinitionAlreadyMerged_ReturnsWithoutInvokingProcessor()
    {
        // Arrange
        var schema = Schema(FormDefinitionId, MergedMap());
        _schemaRepository.GetByFormIdAsync(TenantId, FormId, Arg.Any<CancellationToken>())
            .Returns(schema);
        ReadsHistoricalDefinition();

        // Act
        var result = await GetOrCompileAsync(HistoricalFormDefinitionId);

        // Assert
        result.Should().BeSameAs(schema);
        await _schemaProcessor.DidNotReceiveWithAnyArgs().ProcessAsync(default, default, default, default, default);
    }

    [Fact]
    public async Task FormSchemaProvider_GetOrCompileAsync_WithHistoricalDefinitionMissingColumns_ReturnsMergedSchema()
    {
        // Arrange
        var merged = Schema(FormDefinitionId, MergedMap());
        _schemaRepository.GetByFormIdAsync(TenantId, FormId, Arg.Any<CancellationToken>())
            .Returns(Schema(FormDefinitionId, CompiledMap(CurrentDefinitionJson)), merged);
        ReadsHistoricalDefinition();

        // Act
        var result = await GetOrCompileAsync(HistoricalFormDefinitionId);

        // Assert
        result.Should().BeSameAs(merged);
        await _schemaProcessor.Received(1).ProcessAsync(
            TenantId,
            FormId,
            HistoricalFormDefinitionId,
            cancellationToken: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FormSchemaProvider_GetOrCompileAsync_WhenProcessorDoesNotCoverRequestedDefinition_ReturnsNull()
    {
        // Arrange
        _schemaRepository.GetByFormIdAsync(TenantId, FormId, Arg.Any<CancellationToken>())
            .Returns((FormSchemaEntity?)null, Schema(1, FormSchemaEntity.EmptyFlatteningMapJson));

        // Act
        var result = await GetOrCompileAsync(FormDefinitionId);

        // Assert
        result.Should().BeNull();
        await _schemaProcessor.Received(1).ProcessAsync(
            TenantId,
            FormId,
            FormDefinitionId,
            cancellationToken: Arg.Any<CancellationToken>());
    }

    private Task<FormSchemaEntity?> GetOrCompileAsync(long formDefinitionId)
    {
        FormSchemaProvider provider = new(_schemaRepository, _schemaProcessor, _formsRepository, _compiler);
        return provider.GetOrCompileAsync(TenantId, FormId, formDefinitionId, TestContext.Current.CancellationToken);
    }

    private void ReadsHistoricalDefinition() =>
        _formsRepository
            .SingleOrDefaultAsync(Arg.Any<DefinitionByFormAndDefinitionIdSpec>(), Arg.Any<CancellationToken>())
            .Returns(new FormDefinition(TenantId, jsonData: HistoricalDefinitionJson) { Id = HistoricalFormDefinitionId });

    private string CompiledMap(string definitionJson) => _compiler.CompilePersisted(definitionJson).FlatteningMapJson;

    private string MergedMap() =>
        _compiler.CompilePersisted(CurrentDefinitionJson, CompiledMap(HistoricalDefinitionJson)).FlatteningMapJson;

    private static FormSchemaEntity Schema(long formDefinitionRevision, string flatteningMapJson) =>
        new(TenantId, FormId, formDefinitionRevision, flatteningMapJson, FormSchemaEntity.EmptyCodebookJson);
}
