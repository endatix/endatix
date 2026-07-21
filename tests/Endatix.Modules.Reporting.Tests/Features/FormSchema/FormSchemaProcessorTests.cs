using System.Text.Json;
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
            schema.Codebook.Contains("version") &&
            schema.Locales.Contains("default")),
        Arg.Any<CancellationToken>());
  }

  [Fact]
  public async Task FormSchemaProcessor_ProcessAsync_WithLocalizedTitles_ReplacesLocalesOnUpdate()
  {
    IFormsRepository formsRepository = Substitute.For<IFormsRepository>();
    IFormSchemaRepository schemaRepository = Substitute.For<IFormSchemaRepository>();
    FormDefinition definitionWithEs = CreateFormDefinition(
        FormDefinitionId,
        """
            {
              "pages": [
                {
                  "name": "p1",
                  "elements": [
                    {
                      "type": "text",
                      "name": "q1",
                      "title": { "default": "Name", "es": "Nombre", "en": "Name" }
                    }
                  ]
                }
              ]
            }
            """);
    FormSchemaEntity existing = new(
        TenantId,
        FormId,
        FormDefinitionId,
        FormSchemaEntity.EmptyFlatteningMapJson,
        FormSchemaEntity.EmptyCodebookJson,
        """["default","es","fr"]""");
    formsRepository.SingleOrDefaultAsync(Arg.Any<DefinitionByFormAndDefinitionIdSpec>(), Arg.Any<CancellationToken>())
        .Returns(definitionWithEs);
    schemaRepository.GetByFormIdAsync(TenantId, FormId, Arg.Any<CancellationToken>())
        .Returns(existing);

    FormSchemaProcessor processor = new(
        formsRepository,
        schemaRepository,
        new FormSchemaCompiler(),
        NullLogger<FormSchemaProcessor>.Instance);

    await processor.ProcessAsync(TenantId, FormId, FormDefinitionId, TestContext.Current.CancellationToken);

    existing.Locales.Should().Contain("default");
    existing.Locales.Should().Contain("es");
    existing.Locales.Should().Contain("en");
    existing.Locales.Should().NotContain("fr");
    await schemaRepository.Received(1).SaveAsync(existing, Arg.Any<CancellationToken>());
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
    // Arrange
    IFormsRepository formsRepository = Substitute.For<IFormsRepository>();
    IFormSchemaRepository schemaRepository = Substitute.For<IFormSchemaRepository>();
    FormDefinition historicalDefinition = CreateFormDefinition(
        HistoricalFormDefinitionId,
        """
            {
              "pages": [
                {
                  "name": "p1",
                  "elements": [
                    { "type": "text", "name": "q1", "title": "Question 1" }
                  ]
                }
              ]
            }
            """);
    FormSchemaCompiler compiler = new();
    FormSchemaCompileResult existingCompiled = compiler.CompilePersisted(
        """
            {
              "pages": [
                {
                  "name": "p1",
                  "elements": [
                    {
                      "type": "checkbox",
                      "name": "q2",
                      "title": "Favorite colors",
                      "choices": [
                        { "value": "red", "text": "Red" },
                        { "value": "blue", "text": "Blue" }
                      ]
                    },
                    {
                      "type": "matrixdropdown",
                      "name": "q3",
                      "title": "Team ratings",
                      "columns": [
                        { "name": "score", "title": "Score" }
                      ],
                      "choices": [1, 2, 3],
                      "rows": [
                        { "value": "alice", "text": "Alice" }
                      ]
                    }
                  ]
                }
              ]
            }
            """);
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

    // Act
    await processor.ProcessAsync(TenantId, FormId, HistoricalFormDefinitionId, TestContext.Current.CancellationToken);

    // Assert
    existing.FormDefinitionRevision.Should().Be(FormDefinitionId);
    existing.FlatteningMap.Should().Contain("q1");
    existing.FlatteningMap.Should().Contain("q2__red");
    existing.FlatteningMap.Should().Contain("q3__alice__score");
    await schemaRepository.Received(1).SaveAsync(existing, Arg.Any<CancellationToken>());

    using JsonDocument codebook = JsonDocument.Parse(existing.Codebook);
    JsonElement columns = codebook.RootElement.GetProperty("columns");

    JsonElement redColumn = columns.GetProperty("q2__red");
    redColumn.GetProperty("surveyJsType").GetString().Should().Be("checkbox");
    redColumn.GetProperty("exportShape").GetString().Should().Be("multiple_response");
    redColumn.GetProperty("choiceValue").GetString().Should().Be("red");
    redColumn.GetProperty("choiceLabel").GetProperty("default").GetString().Should().Be("Red");
    redColumn.GetProperty("title").GetProperty("default").GetString().Should().Be("Favorite colors");

    JsonElement matrixCellColumn = columns.GetProperty("q3__alice__score");
    matrixCellColumn.GetProperty("surveyJsType").GetString().Should().Be("matrixdropdown");
    matrixCellColumn.GetProperty("exportShape").GetString().Should().Be("matrix_cell");
    matrixCellColumn.GetProperty("matrixRowValue").GetString().Should().Be("alice");
    matrixCellColumn.GetProperty("matrixColumnValue").GetString().Should().Be("score");
    matrixCellColumn.GetProperty("rowLabel").GetProperty("default").GetString().Should().Be("Alice");
    matrixCellColumn.GetProperty("columnLabel").GetProperty("default").GetString().Should().Be("Score");
    matrixCellColumn.GetProperty("title").GetProperty("default").GetString().Should().Be("Team ratings");
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
