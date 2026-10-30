using System.Text.Json;
using Endatix.Core.Entities;
using Endatix.Modules.Reporting.Features.FormSchema.Codebook;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;
using Endatix.Modules.Reporting.Shared.SurveyJs;

namespace Endatix.Modules.Reporting.Features.Export.Integrations.Crunch.Shoji;

/// <summary>
/// The Shoji <c>order</c>, grouped into the folders of <see cref="ShojiFolderNames"/>, and the <c>hidden</c> list.
/// </summary>
internal static partial class ShojiCodebookGenerator
{
    /// <summary>An alias or a folder of entries: <c>{"group": name, "entities": [...]}</c>.</summary>
    private interface IShojiOrderEntry
    {
        void WriteTo(Utf8JsonWriter writer);
    }

    private sealed record ShojiOrderAlias(string Alias) : IShojiOrderEntry
    {
        public void WriteTo(Utf8JsonWriter writer) => writer.WriteStringValue(Alias);
    }

    private sealed record ShojiFolder(string Name, List<IShojiOrderEntry> Entries) : IShojiOrderEntry
    {
        public static ShojiFolder Of(string name, IEnumerable<string> aliases) =>
            new(name, [.. aliases.Select(alias => new ShojiOrderAlias(alias))]);

        public void WriteTo(Utf8JsonWriter writer)
        {
            writer.WriteStartObject();
            writer.WriteString(ShojiCodebookPropertyNames.Group, Name);
            writer.WritePropertyName(ShojiCodebookPropertyNames.Entities);
            WriteOrderEntries(writer, Entries);
            writer.WriteEndObject();
        }
    }

    /// <summary>The variables written, and which of them no question owns.</summary>
    private sealed record ShojiOrderSources(
        ShojiCodebookWriteArgs Args,
        IReadOnlyList<string> WrittenAliases,
        NonQuestionAliases NonQuestionAliases)
    {
        public HashSet<string> Written { get; } = new(WrittenAliases, StringComparer.Ordinal);
    }

    /// <summary>The Questions folder and its panel and loop sub-folders by name.</summary>
    private sealed class QuestionFolders
    {
        private readonly Dictionary<string, ShojiFolder> _subFolders = new(StringComparer.Ordinal);

        public ShojiFolder Root { get; } = new(ShojiFolderNames.Questions, []);

        // A sub-folder takes its place in Questions where its first alias appears.
        public ShojiFolder For(string? subFolderName)
        {
            if (string.IsNullOrWhiteSpace(subFolderName))
            {
                return Root;
            }

            if (!_subFolders.TryGetValue(subFolderName, out var folder))
            {
                folder = new ShojiFolder(subFolderName, []);
                _subFolders.Add(subFolderName, folder);
                Root.Entries.Add(folder);
            }

            return folder;
        }
    }

    private static readonly string[] _systemColumnOrder =
    [
        SubmissionExportRow.SystemColumns.FormId,
        SubmissionExportRow.SystemColumns.Id,
        SubmissionExportRow.SystemColumns.IsComplete,
        SubmissionExportRow.SystemColumns.CollectionStatus,
        SubmissionExportRow.SystemColumns.CreatedAt,
        SubmissionExportRow.SystemColumns.ModifiedAt,
        SubmissionExportRow.SystemColumns.StartedAt,
        SubmissionExportRow.SystemColumns.CompletedAt,
        SubmissionExportRow.SystemColumns.DurationSeconds,
        SubmissionExportRow.SystemColumns.SubmitterId,
        SubmissionExportRow.SystemColumns.SubmitterDisplayId,
    ];

    // Crunch refuses an alias that is both hidden and ordered ("FormId cannot be in multiple orders").
    private static readonly string[] _hiddenSystemColumns = [SubmissionExportRow.SystemColumns.FormId];

    private static void WriteOrderAndHidden(Utf8JsonWriter writer, ShojiOrderSources sources)
    {
        writer.WritePropertyName(ShojiCodebookPropertyNames.Order);
        WriteOrderEntries(writer, BuildFolders(sources));

        var hidden = _hiddenSystemColumns.Where(sources.Written.Contains).ToList();
        if (hidden.Count > 0)
        {
            writer.WritePropertyName(ShojiCodebookPropertyNames.Hidden);
            WriteOrderEntries(writer, hidden.Select(alias => new ShojiOrderAlias(alias)));
        }
    }

    /// <summary>
    /// System columns, audience values, submitter profile fields, calculated values, then the questions. The CSV keeps
    /// the non-question columns last; Crunch matches CSV columns to variables by alias, so the orders may differ. An
    /// empty folder is left out.
    /// </summary>
    private static IEnumerable<IShojiOrderEntry> BuildFolders(ShojiOrderSources sources)
    {
        HashSet<string> placed = new(_hiddenSystemColumns, StringComparer.Ordinal);
        var nonQuestion = sources.NonQuestionAliases;
        ShojiFolder[] folders =
        [
            ShojiFolder.Of(ShojiFolderNames.MetaData, _systemColumnOrder.Where(alias => sources.Written.Contains(alias) && placed.Add(alias))),
            ShojiFolder.Of(ShojiFolderNames.ParticipantVariables, nonQuestion.Personalization.Where(placed.Add)),
            ShojiFolder.Of(ShojiFolderNames.Submitter, nonQuestion.SubmitterProfile.Where(placed.Add)),
            ShojiFolder.Of(ShojiFolderNames.CalculatedValues, nonQuestion.Calculated.Where(placed.Add)),
            BuildQuestionsFolder(sources, placed),
        ];
        return folders.Where(folder => folder.Entries.Count > 0);
    }

    /// <summary>
    /// The first Shoji alias of each flattening-map column in compile order (page/element walk, not SurveyJS
    /// visibleIndex), in the sub-folder of its panel or loop. Aliases no column led to go last.
    /// </summary>
    private static ShojiFolder BuildQuestionsFolder(ShojiOrderSources sources, HashSet<string> placed)
    {
        QuestionFolders folders = new();
        foreach (var column in sources.Args.FlatteningMap.Columns)
        {
            folders.For(SubFolderName(column, sources.Args.Questions)).Entries.AddRange(
                OrderAliases(column, sources.Args)
                    .Where(alias => sources.Written.Contains(alias) && placed.Add(alias))
                    .Select(alias => new ShojiOrderAlias(alias)));
        }

        folders.Root.Entries.AddRange(sources.WrittenAliases.Where(placed.Add).Select(alias => new ShojiOrderAlias(alias)));
        folders.Root.Entries.RemoveAll(entry => entry is ShojiFolder { Entries.Count: 0 });
        return folders.Root;
    }

    // Folders take default-locale titles, as the codebook's parentPanelTitle does. A loop is named by its own panel,
    // so every alias of the loop, a panel nested in it included, is in one sub-folder.
    private static string? SubFolderName(FormSchemaColumn column, IReadOnlyDictionary<string, JsonElement> questions)
    {
        if (column.LoopPath is { Count: > 0 } loopPath)
        {
            var loopName = loopPath[0].PanelValueName;
            return questions.TryGetValue(loopName, out var loop) && ReadDefaultTitle(loop) is { Length: > 0 } title
                ? title
                : loopName;
        }

        return questions.TryGetValue(column.SourceQuestion ?? column.Key, out var question)
            ? ReadParentPanelTitle(question)
            : null;
    }

    private static string? ReadDefaultTitle(JsonElement question)
    {
        if (!question.TryGetProperty(SurveyJsPropertyNames.Title, out var title))
        {
            return null;
        }

        return title.ValueKind switch
        {
            JsonValueKind.String => title.GetString(),
            JsonValueKind.Object when title.TryGetProperty(FormSchemaCodebookPropertyNames.Default, out var text) &&
                                      text.ValueKind == JsonValueKind.String => text.GetString(),
            _ => null,
        };
    }

    private static IEnumerable<string> OrderAliases(FormSchemaColumn column, ShojiCodebookWriteArgs args)
    {
        if (!IsNeutralRangeSliderColumn(column, args.Questions))
        {
            return ResolveShojiOrderAlias(column, args.Questions, args.KeySeparator) is { } alias ? [alias] : [];
        }

        return ((string[])["min", "max"]).Select(bound => ExportKeyTransformer.Transform(
            ExportPathBuilder.Join(column.Key, bound),
            args.KeySeparator));
    }

    private static void WriteOrderEntries(Utf8JsonWriter writer, IEnumerable<IShojiOrderEntry> entries)
    {
        writer.WriteStartArray();
        foreach (var entry in entries)
        {
            entry.WriteTo(writer);
        }

        writer.WriteEndArray();
    }
}
