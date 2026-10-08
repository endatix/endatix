using Endatix.IntegrationTests.Shared;
using Endatix.Modules.Reporting.Data;
using static Endatix.IntegrationTests.FormSchemaRebuildWorld;

namespace Endatix.IntegrationTests;

/// <summary>
/// What a form's schema holds after rebuilds against PostgreSQL: rebuilds that overlap, flattens of submissions made
/// on an older definition than the schema, and definitions that change while a rebuild runs.
/// </summary>
[Collection(nameof(DbIntegrationTestCollection))]
[Trait("Category", "Infrastructure")]
[Trait("Priority", "P1")]
[Trait("DbSpecific", "PostgreSql")]
public sealed class FormSchemaRebuildIntegrationTests(DbIntegrationFixture fixture)
{
    private readonly FormSchemaRebuildWorld _world = new(fixture);

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Concurrent_rebuilds_of_one_form_keep_every_question_either_adds(bool schemaAlreadyCompiled)
    {
        // Arrange
        var form = await _world.SeedFormWithRealSubmissionAsync([BaseDefinition, AddsQuestionA, AddsQuestionB]);
        if (schemaAlreadyCompiled)
        {
            await _world.CompileAsync(form.FormId, form.DefinitionIds[0]);
        }

        Rendezvous bothRead = new(parties: 2);

        // Act
        await Task.WhenAll(
            _world.CompileAsync(form.FormId, form.DefinitionIds[1], schemas => new WaitAfterLockedRead(schemas, bothRead)),
            _world.CompileAsync(form.FormId, form.DefinitionIds[2], schemas => new WaitAfterLockedRead(schemas, bothRead)));

        // Assert
        var schema = await _world.ReadSchemaAsync(form.FormId);
        ColumnKeys(schema).Should().Contain(["q0", "qa", "qb"]);
        schema.FormDefinitionRevision.Should().Be(form.DefinitionIds[2]);
    }

    [Fact]
    public async Task Flattening_a_submission_on_an_older_definition_keeps_the_schema_compiled_from_the_newer_one()
    {
        // Arrange
        var form = await _world.SeedFormWithRealSubmissionAsync([BaseDefinition, RetitlesBase]);
        await _world.CompileAsync(form.FormId, form.DefinitionIds[0]);
        await _world.CompileAsync(form.FormId, form.DefinitionIds[1]);
        var compiled = await _world.ReadSchemaAsync(form.FormId);

        // Act
        await _world.FlattenAsync(form.FormId, form.SubmissionId);

        // Assert
        var afterFlatten = await _world.ReadSchemaAsync(form.FormId);
        afterFlatten.ModifiedAt.Should().Be(compiled.ModifiedAt);
        afterFlatten.Codebook.Should().Be(compiled.Codebook).And.Contain("Base, retitled");
        var row = await _world.ReadFlattenedAsync(form.SubmissionId);
        row.DataJson.Should().Contain("first answer");
    }

    [Fact]
    public async Task A_test_submission_on_an_older_version_does_not_drop_the_newer_versions_columns()
    {
        // Arrange — no real submissions, so both versions compile by replacing the schema.
        var form = await _world.SeedFormAsync([AddsQuestionA, AddsQuestionB]);
        await _world.CompileAsync(form.FormId, form.DefinitionIds[0]);
        await _world.CompileAsync(form.FormId, form.DefinitionIds[1]);
        var onOlder = await _world.AddTestSubmissionAsync(form.FormId, form.DefinitionIds[0], """{"q0":"old","qa":"answer a"}""");
        var onNewer = await _world.AddTestSubmissionAsync(form.FormId, form.DefinitionIds[1], """{"q0":"new","qb":"answer b"}""");
        await _world.FlattenAsync(form.FormId, onOlder);

        // Act
        await _world.FlattenAsync(form.FormId, onNewer);

        // Assert
        var row = await _world.ReadFlattenedAsync(onNewer);
        row.DataJson.Should().Contain("answer b");
        var schema = await _world.ReadSchemaAsync(form.FormId);
        ColumnKeys(schema).Should().Contain(["q0", "qa", "qb"]);
        schema.FormDefinitionRevision.Should().Be(form.DefinitionIds[1]);
    }

    [Fact]
    public async Task A_forced_replace_from_an_older_definition_lets_the_newer_ones_submissions_bring_their_columns_back()
    {
        // Arrange — the older definition is made active again and the schema replaced from it.
        var form = await _world.SeedFormWithRealSubmissionAsync([AddsQuestionA, AddsQuestionB]);
        await _world.CompileAsync(form.FormId, form.DefinitionIds[1]);
        var onNewer = await _world.AddTestSubmissionAsync(form.FormId, form.DefinitionIds[1], """{"q0":"new","qb":"answer b"}""");
        await _world.ReplaceAsync(form.FormId, form.DefinitionIds[0]);

        // Act
        await _world.FlattenAsync(form.FormId, onNewer);

        // Assert
        (await _world.ReadFlattenedAsync(onNewer)).DataJson.Should().Contain("answer b");
    }

    [Fact]
    public async Task A_rebuild_that_read_the_definition_before_an_edit_ends_with_the_edited_columns()
    {
        // Arrange — no real submissions, so each rebuild replaces the schema. After this rebuild read the definition,
        // it is edited in place and the edit's own rebuild commits first.
        var form = await _world.SeedFormAsync([BaseDefinition]);
        await _world.CompileAsync(form.FormId, form.DefinitionIds[0]);
        Func<IFormSchemaRepository, IFormSchemaRepository> editFirst = schemas => new BeforeLockedRead(schemas, async () =>
        {
            await _world.EditDefinitionAsync(form.DefinitionIds[0], AddsQuestionA);
            await _world.CompileAsync(form.FormId, form.DefinitionIds[0]);
        });

        // Act
        await _world.CompileAsync(form.FormId, form.DefinitionIds[0], editFirst);

        // Assert
        ColumnKeys(await _world.ReadSchemaAsync(form.FormId)).Should().Contain(["q0", "qa"]);
    }
}
