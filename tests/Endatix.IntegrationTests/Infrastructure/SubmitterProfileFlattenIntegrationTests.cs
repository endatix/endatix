using System.Text.Json;
using Endatix.IntegrationTests.Shared;
using Endatix.Modules.Reporting.Features.FormSchema.SubmitterProfile;
using static Endatix.IntegrationTests.FormSchemaRebuildWorld;

namespace Endatix.IntegrationTests;

/// <summary>
/// Submitter profile fields in prepared submissions against the database: the columns a listed field gets, and the
/// flatten a submitter change on a completed submission runs again.
/// </summary>
[Collection(nameof(DbIntegrationTestCollection))]
[Trait("Category", "Infrastructure")]
[Trait("Priority", "P1")]
public sealed class SubmitterProfileFlattenIntegrationTests(DbIntegrationFixture fixture)
{
    private const string Profile = """{"email":"ana@example.com","phone":"+1 555 0100"}""";

    private readonly FormSchemaRebuildWorld _world = new(fixture, new SubmitterProfileFields(["email"]));

    [Fact]
    public async Task A_submitter_change_on_a_completed_submission_flattens_its_row_again_with_the_listed_fields()
    {
        // Arrange
        var form = await _world.SeedFormWithRealSubmissionAsync([BaseDefinition]);
        await _world.CompileAsync(form.FormId, form.DefinitionIds[0]);
        await _world.FlattenAsync(form.FormId, form.SubmissionId);
        var flattenedBefore = Values(await _world.ReadFlattenedAsync(form.SubmissionId));
        var change = await _world.ChangeSubmitterAsync(form.SubmissionId, Profile);

        // Act
        await using var scope = _world.OpenScope();
        var result = await _world.RunFlattenJobAsync(change, scope.TransactionLock);

        // Assert
        result.IsSuccess.Should().BeTrue();
        flattenedBefore.GetProperty("submitter_email").ValueKind.Should().Be(JsonValueKind.Null);
        var flattened = await _world.ReadFlattenedAsync(form.SubmissionId);
        Values(flattened).GetProperty("submitter_email").GetString().Should().Be("ana@example.com");
        flattened.DataJson.Should().NotContain("+1 555 0100", "only listed fields reach reporting");
    }

    [Fact]
    public async Task A_flatten_after_a_field_is_listed_adds_its_column_to_a_schema_compiled_without_it()
    {
        // Arrange
        FormSchemaRebuildWorld beforeListing = new(fixture);
        var form = await beforeListing.SeedFormWithRealSubmissionAsync([BaseDefinition]);
        await beforeListing.CompileAsync(form.FormId, form.DefinitionIds[0]);
        await _world.ChangeSubmitterAsync(form.SubmissionId, Profile);

        // Act
        await _world.FlattenAsync(form.FormId, form.SubmissionId);

        // Assert
        var schema = await _world.ReadSchemaAsync(form.FormId);
        ColumnKeys(schema).Should().Contain(["q0", "submitter_email"]);
        schema.Codebook.Should().Contain("\"submitter_profile\"");
        Values(await _world.ReadFlattenedAsync(form.SubmissionId)).GetProperty("submitter_email").GetString()
            .Should().Be("ana@example.com");
    }

    private static JsonElement Values(Modules.Reporting.Domain.FlattenedSubmission row)
    {
        using var document = JsonDocument.Parse(row.DataJson!);
        return document.RootElement.Clone();
    }
}
