using System.Text.Json;
using System.Text.Json.Nodes;
using Endatix.Core.UseCases.Submissions;
using Endatix.Modules.Reporting.Contracts.Export;
using Endatix.Modules.Reporting.Features.Export;
using Endatix.Modules.Reporting.Features.Export.Integrations.Crunch.Shoji;
using Endatix.Modules.Reporting.Features.FlattenedSubmission;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;
using Endatix.Modules.Reporting.Features.FormSchema.Personalization;
using Endatix.Modules.Reporting.Features.FormSchema.SubmitterProfile;
using Endatix.Modules.Reporting.Tests.Features.FormSchema.FormSchema;
using FormSchemaEntity = Endatix.Modules.Reporting.Domain.FormSchema;

namespace Endatix.Modules.Reporting.Tests.Features.FormSchema.Personalization;

/// <summary>
/// One response of a personalization fixture: its answers, the snapshot frozen on it if any, the system values its
/// export row carries (<c>system</c>, optional), and its submitter profile snapshot (<c>submitterProfile</c>, optional).
/// </summary>
internal sealed record FixtureResponse(
    string Id,
    string AnswersJson,
    string? Snapshot,
    JsonElement System = default,
    string? SubmitterProfile = null)
{
    public PersonalizationRead? Personalization => PersonalizationReader.Read(Snapshot);
}

/// <summary>
/// Plays responses through the same steps production runs, without a database: a definition compile, then for each
/// flatten the audience-column include (<see cref="FormSchemaCompiler.IncludePersonalization"/>) and the flatten
/// with the schema as it stands after that include. Given submitter profile fields, it compiles and flattens them too.
/// </summary>
internal sealed class PersonalizationPipeline
{
    private readonly FormSchemaCompiler _compiler;
    private readonly SubmitterProfileFields _submitterProfileFields;
    private readonly Dictionary<string, Dictionary<string, JsonElement?>> _rows = new(StringComparer.Ordinal);

    private PersonalizationPipeline(SubmitterProfileFields submitterProfileFields, string definitionJson)
    {
        _submitterProfileFields = submitterProfileFields;
        _compiler = new FormSchemaCompiler(submitterProfileFields: submitterProfileFields);
        Schema = _compiler.CompilePersisted(definitionJson);
    }

    public FormSchemaCompileResult Schema { get; private set; }

    /// <summary>Each response's flattened row, as written when it was flattened.</summary>
    public IReadOnlyDictionary<string, Dictionary<string, JsonElement?>> Rows => _rows;

    public static PersonalizationPipeline Compile(string definitionJson, SubmitterProfileFields? submitterProfileFields = null) =>
        new(submitterProfileFields ?? SubmitterProfileFields.None, definitionJson);

    public static IReadOnlyList<FixtureResponse> LoadResponses() =>
        LoadResponses(Path.Combine("Personalization", "personalization-responses.json"));

    /// <summary>
    /// Responses from a fixture file under Fixtures/. A response gives its answers inline (<c>answers</c>) or as a
    /// fixture path (<c>answersFile</c>), so a large form can reuse its existing submission.
    /// </summary>
    public static IReadOnlyList<FixtureResponse> LoadResponses(string fixturePath)
    {
        var responses = FormSchemaFixtureLoader.LoadJson(fixturePath);
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
        ResponseSnapshots snapshots = new(response.Personalization, _submitterProfileFields.Read(response.SubmitterProfile));
        _rows[response.Id] = FlattenedSubmissionFlattener.Flatten(answers.RootElement, Schema.FlatteningMap, snapshots)
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

    /// <summary>
    /// The rows the reporting read model streams to an export, one per flattened response, with the system values the
    /// fixture gives it (<c>system</c>), or a complete response with no submitter.
    /// </summary>
    public IReadOnlyList<FlattenedExportRow> ExportRows(IReadOnlyList<FixtureResponse> responses) =>
        responses.Select((response, index) => ExportRow(response, index + 1)).ToList();

    public FormSchemaEntity Entity(long formId = 100) => new(1, formId, 1, Schema.FlatteningMapJson, Schema.CodebookJson);

    private FlattenedExportRow ExportRow(FixtureResponse response, long submissionId)
    {
        var system = new FixtureSystemValues(response.System);
        DateTime created = new(2026, 10, 14, 10, 0, 0, DateTimeKind.Utc);
        return new FlattenedExportRow(
            SubmissionId: submissionId,
            FormId: 100,
            IsComplete: system.IsComplete,
            CollectionStatus: system.IsComplete ? "complete" : "in_progress",
            CreatedAt: created,
            ModifiedAt: created,
            StartedAt: system.StartedAt,
            CompletedAt: system.CompletedAt,
            SubmitterId: system.SubmitterId,
            SubmitterDisplayId: system.SubmitterDisplayId,
            DataJson: JsonSerializer.Serialize(_rows[response.Id]));
    }

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


    private static FixtureResponse ToResponse(JsonElement response)
    {
        var snapshot = response.GetProperty("snapshot");
        return new FixtureResponse(
            response.GetProperty("id").GetString()!,
            AnswersOf(response),
            snapshot.ValueKind == JsonValueKind.Null ? null : snapshot.GetRawText(),
            response.TryGetProperty("system", out var system) ? system.Clone() : default,
            response.TryGetProperty("submitterProfile", out var profile) ? profile.GetRawText() : null);
    }

    // answersFile is the base; answers adds to it or replaces its values, so a response can reuse a large submission.
    private static string AnswersOf(JsonElement response)
    {
        var answers = response.TryGetProperty("answersFile", out var answersFile)
            ? JsonNode.Parse(FormSchemaFixtureLoader.LoadText(answersFile.GetString()!))!.AsObject()
            : [];
        if (response.TryGetProperty("answers", out var extra))
        {
            foreach (var (name, value) in JsonNode.Parse(extra.GetRawText())!.AsObject())
            {
                answers[name] = value?.DeepClone();
            }
        }

        return answers.ToJsonString();
    }
}

/// <summary>A fixture response's <c>system</c> values; absent ones read as a complete response with no submitter.</summary>
internal readonly record struct FixtureSystemValues(JsonElement System)
{
    public bool IsComplete => !Has("isComplete") || System.GetProperty("isComplete").GetBoolean();

    public DateTime? StartedAt => Date("startedAt");

    public DateTime? CompletedAt => Date("completedAt");

    public long? SubmitterId => Has("submitterId") ? System.GetProperty("submitterId").GetInt64() : null;

    public string? SubmitterDisplayId => Has("submitterDisplayId") ? System.GetProperty("submitterDisplayId").GetString() : null;

    private bool Has(string name) =>
        System.ValueKind == JsonValueKind.Object &&
        System.TryGetProperty(name, out var value) &&
        value.ValueKind != JsonValueKind.Null;

    private DateTime? Date(string name) =>
        Has(name) ? System.GetProperty(name).GetDateTime().ToUniversalTime() : null;
}
