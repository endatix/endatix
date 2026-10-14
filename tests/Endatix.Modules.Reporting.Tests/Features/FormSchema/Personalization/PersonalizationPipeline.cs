using System.Text.Json;
using Endatix.Core.UseCases.Submissions;
using Endatix.Modules.Reporting.Contracts.Export;
using Endatix.Modules.Reporting.Features.Export;
using Endatix.Modules.Reporting.Features.Export.Integrations.Crunch.Shoji;
using Endatix.Modules.Reporting.Features.FlattenedSubmission;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;
using Endatix.Modules.Reporting.Features.FormSchema.Personalization;
using Endatix.Modules.Reporting.Tests.Features.FormSchema.FormSchema;
using FormSchemaEntity = Endatix.Modules.Reporting.Domain.FormSchema;

namespace Endatix.Modules.Reporting.Tests.Features.FormSchema.Personalization;

/// <summary>One response of the personalization fixture: its answers and the snapshot frozen on it, if any.</summary>
internal sealed record FixtureResponse(string Id, string AnswersJson, string? Snapshot)
{
    public PersonalizationRead? Personalization => PersonalizationReader.Read(Snapshot);
}

/// <summary>
/// Plays responses through the same steps production runs, without a database: a definition compile, then for each
/// flatten the audience-column include (<see cref="FormSchemaCompiler.IncludePersonalization"/>) and the flatten
/// with the schema as it stands after that include.
/// </summary>
internal sealed class PersonalizationPipeline
{
    private readonly FormSchemaCompiler _compiler = new();
    private readonly Dictionary<string, Dictionary<string, JsonElement?>> _rows = new(StringComparer.Ordinal);

    private PersonalizationPipeline(FormSchemaCompileResult schema) => Schema = schema;

    public FormSchemaCompileResult Schema { get; private set; }

    /// <summary>Each response's flattened row, as written when it was flattened.</summary>
    public IReadOnlyDictionary<string, Dictionary<string, JsonElement?>> Rows => _rows;

    public static PersonalizationPipeline Compile(string definitionJson) =>
        new(new FormSchemaCompiler().CompilePersisted(definitionJson));

    public static IReadOnlyList<FixtureResponse> LoadResponses()
    {
        var responses = FormSchemaFixtureLoader.LoadPersonalization("personalization-responses.json");
        return responses.EnumerateArray().Select(ToResponse).ToList();
    }

    /// <summary>A definition change: merge keeps every column, replace keeps only the definition's.</summary>
    public void Recompile(string definitionJson, FormSchemaCompileMode mode) =>
        Schema = _compiler.CompilePersisted(definitionJson, Schema.FlatteningMapJson, Schema.CodebookJson, mode);

    public void Flatten(string definitionJson, FixtureResponse response)
    {
        var columns = PersonalizationColumns.From(response.Personalization);
        if (columns.Count > 0 && !PersonalizationColumns.AreCoveredBy(Schema.FlatteningMap, columns))
        {
            Schema = _compiler.IncludePersonalization(
                definitionJson,
                new PersistedSchemaJson(Schema.FlatteningMapJson, Schema.CodebookJson),
                columns);
        }

        using var answers = JsonDocument.Parse(response.AnswersJson);
        _rows[response.Id] = FlattenedSubmissionFlattener.Flatten(answers.RootElement, Schema.FlatteningMap, response.Personalization)
            .ToDictionary(entry => entry.Key, entry => entry.Value?.Clone(), StringComparer.Ordinal);
    }

    public void FlattenAll(string definitionJson, IEnumerable<FixtureResponse> responses)
    {
        foreach (var response in responses)
        {
            Flatten(definitionJson, response);
        }
    }

    public IReadOnlyList<string> ExportKeys(ColumnAliasProfile profile = ColumnAliasProfile.Native) =>
        ExportColumnPlanBuilder.Build(Entity(), aliasProfile: profile).Columns.Select(column => column.ExportKey).ToList();

    public string ShojiCodebook() =>
        ShojiCodebookGenerator.Generate(
            Schema.FlatteningMapJson,
            Schema.CodebookJson,
            ExportFormatSettings.InterimCrunchKeySeparator);

    /// <summary>Key and data type of every audience column, in schema order.</summary>
    public IReadOnlyList<(string Key, string DataType)> PersonalizationColumnTypes() =>
        Schema.FlatteningMap.Columns
            .Where(column => column.Kind is FormSchemaColumnKind.Personalization)
            .Select(column => (column.Key, column.DataType))
            .ToList();

    private FormSchemaEntity Entity() => new(1, 100, 1, Schema.FlatteningMapJson, Schema.CodebookJson);

    private static FixtureResponse ToResponse(JsonElement response)
    {
        var snapshot = response.GetProperty("snapshot");
        return new FixtureResponse(
            response.GetProperty("id").GetString()!,
            response.GetProperty("answers").GetRawText(),
            snapshot.ValueKind == JsonValueKind.Null ? null : snapshot.GetRawText());
    }
}
