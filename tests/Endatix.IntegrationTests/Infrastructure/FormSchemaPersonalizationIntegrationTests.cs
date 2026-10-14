using Endatix.IntegrationTests.Shared;
using static Endatix.IntegrationTests.FormSchemaRebuildWorld;

namespace Endatix.IntegrationTests;

/// <summary>
/// Audience columns in a form's schema against the database: flattens that add them at once, definition rebuilds
/// between flattens, and a forced replace followed by a backfill.
/// </summary>
[Collection(nameof(DbIntegrationTestCollection))]
[Trait("Category", "Infrastructure")]
[Trait("Priority", "P1")]
public sealed class FormSchemaPersonalizationIntegrationTests(DbIntegrationFixture fixture)
{
    private const string IdentifierKey = "AudienceIdentifier";
    private const string PersonalizationPrefix = "audience_";

    private readonly FormSchemaRebuildWorld _world = new(fixture);

    [Fact]
    public async Task Concurrent_flattens_carrying_different_variables_keep_every_audience_column()
    {
        // Arrange
        var form = await _world.SeedFormWithRealSubmissionAsync([BaseDefinition]);
        await _world.CompileAsync(form.FormId, form.DefinitionIds[0]);
        var withCity = await AddPersonalizedAsync(form, "CG-1", "\"city\":\"Columbus\"");
        var withAge = await AddPersonalizedAsync(form, "CG-2", "\"age\":65");
        Rendezvous bothRead = new(parties: 2);

        // Act
        await Task.WhenAll(
            _world.FlattenAsync(form.FormId, withCity, schemas => new WaitAfterLockedRead(schemas, bothRead)),
            _world.FlattenAsync(form.FormId, withAge, schemas => new WaitAfterLockedRead(schemas, bothRead)));

        // Assert
        var schema = await _world.ReadSchemaAsync(form.FormId);
        ColumnKeys(schema).Should().Contain(["q0", "AudienceIdentifier", "audience_city", "audience_age"]);
        (await _world.ReadFlattenedAsync(withCity)).DataJson.Should().Contain("Columbus");
        (await _world.ReadFlattenedAsync(withAge)).DataJson.Should().Contain("65");
    }

    [Fact]
    public async Task A_definition_rebuild_between_flattens_keeps_the_audience_columns()
    {
        // Arrange
        var form = await _world.SeedFormWithRealSubmissionAsync([BaseDefinition, AddsQuestionA]);
        await _world.CompileAsync(form.FormId, form.DefinitionIds[0]);
        var withCity = await AddPersonalizedAsync(form, "CG-1", "\"city\":\"Columbus\"");
        await _world.FlattenAsync(form.FormId, withCity);

        // Act
        await _world.CompileAsync(form.FormId, form.DefinitionIds[1]);

        // Assert
        var schema = await _world.ReadSchemaAsync(form.FormId);
        ColumnKeys(schema).Should().Contain(["q0", "qa", "AudienceIdentifier", "audience_city"]);
        schema.Codebook.Should().Contain("\"audience_city\"").And.Contain("\"personalization\"");
    }

    [Fact]
    public async Task A_forced_replace_drops_audience_columns_and_a_backfill_brings_back_the_used_ones()
    {
        // Arrange
        var form = await _world.SeedFormWithRealSubmissionAsync([BaseDefinition]);
        await _world.CompileAsync(form.FormId, form.DefinitionIds[0]);
        var withCity = await AddPersonalizedAsync(form, "CG-1", "\"city\":\"Columbus\"");
        await _world.FlattenAsync(form.FormId, withCity);

        // Act
        await _world.ReplaceAsync(form.FormId, form.DefinitionIds[0]);
        var afterReplace = ColumnKeys(await _world.ReadSchemaAsync(form.FormId)).ToList();
        await using (var scope = _world.OpenScope())
        {
            await _world.BackfillAsync(form.FormId, scope.TransactionLock);
        }

        // Assert
        afterReplace.Should().NotContain(key => key.StartsWith(PersonalizationPrefix) || key == IdentifierKey);
        ColumnKeys(await _world.ReadSchemaAsync(form.FormId)).Should().Contain([IdentifierKey, "audience_city"]);
        (await _world.ReadFlattenedAsync(withCity)).DataJson.Should().Contain("Columbus");
    }

    // variablesJson is the snapshot's variables object, without its braces: "city":"Columbus".
    private Task<long> AddPersonalizedAsync(RebuildForm form, string identifier, string variablesJson) =>
        _world.AddPersonalizedSubmissionAsync(
            form.FormId,
            form.DefinitionIds[0],
            $$"""{"identifier":"{{identifier}}","variables":{""" + variablesJson + "}}");
}
