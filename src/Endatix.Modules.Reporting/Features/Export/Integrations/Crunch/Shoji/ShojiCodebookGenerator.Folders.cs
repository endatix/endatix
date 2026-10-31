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

    private sealed class ShojiFolder(string name, List<IShojiOrderEntry> entries) : IShojiOrderEntry
    {
        public string Name { get; } = name;

        public List<IShojiOrderEntry> Entries { get; } = entries;

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

    /// <summary>A panel or loop a question sits in: the panel name it is told apart by, and the title it shows.</summary>
    private sealed record ShojiPanel(string Key, string Label);

    /// <summary>The Questions folder, and a sub-folder for each panel and loop, nested as the panels are.</summary>
    private sealed class QuestionFolders
    {
        private readonly Dictionary<(ShojiFolder Parent, string Key), ShojiFolder> _subFolders = [];
        private readonly Dictionary<ShojiFolder, HashSet<string>> _siblingLabels = [];

        public ShojiFolder Root { get; } = new(ShojiFolderNames.Questions, []);

        // A sub-folder takes its place where its first alias appears. Two panels with one title stay apart, the
        // second numbered ("Details (2)"), so no question moves out of survey order.
        public ShojiFolder For(IReadOnlyList<ShojiPanel> panels)
        {
            var folder = Root;
            foreach (var panel in panels)
            {
                if (!_subFolders.TryGetValue((folder, panel.Key), out var subFolder))
                {
                    subFolder = new ShojiFolder(UniqueLabel(folder, panel.Label.Trim()), []);
                    _subFolders.Add((folder, panel.Key), subFolder);
                    folder.Entries.Add(subFolder);
                }

                folder = subFolder;
            }

            return folder;
        }

        // Numbered until no sibling has the label, so a panel titled "Details (2)" cannot meet a numbered repeat.
        private string UniqueLabel(ShojiFolder parent, string label)
        {
            if (!_siblingLabels.TryGetValue(parent, out var labels))
            {
                labels = new HashSet<string>(StringComparer.Ordinal);
                _siblingLabels.Add(parent, labels);
            }

            var unique = label;
            var repeat = 1;
            while (!labels.Add(unique))
            {
                repeat++;
                unique = $"{label} ({repeat})";
            }

            return unique;
        }
    }

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
    /// System columns, audience values, Submitter fields, calculated values, then the questions. The CSV keeps the
    /// non-question columns last; Crunch matches CSV columns to variables by alias, so the orders may differ. An empty
    /// folder is left out.
    /// </summary>
    private static IEnumerable<IShojiOrderEntry> BuildFolders(ShojiOrderSources sources)
    {
        HashSet<string> placed = new(_hiddenSystemColumns, StringComparer.Ordinal);
        var nonQuestion = sources.NonQuestionAliases;
        ShojiFolder[] folders =
        [
            ShojiFolder.Of(ShojiFolderNames.MetaData, SubmissionExportRow.SystemColumns.OrderedKeys.Where(alias => sources.Written.Contains(alias) && placed.Add(alias))),
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
            var aliases = OrderAliases(column, sources.Args)
                .Where(alias => sources.Written.Contains(alias) && placed.Add(alias))
                .Select(alias => new ShojiOrderAlias(alias))
                .ToList();
            if (aliases.Count > 0)
            {
                folders.For(PanelsOf(column, sources.Args.Questions)).Entries.AddRange(aliases);
            }
        }

        folders.Root.Entries.AddRange(sources.WrittenAliases.Where(placed.Add).Select(alias => new ShojiOrderAlias(alias)));
        return folders.Root;
    }

    // Folders take the codebook's panel titles, which are default-locale. A codebook compiled before parentPanels
    // existed has only the nearest panel's title, so there panels are told apart by title and do not nest. A loop
    // column whose question the codebook lacks goes in a folder named by the loop.
    private static List<ShojiPanel> PanelsOf(FormSchemaColumn column, IReadOnlyDictionary<string, JsonElement> questions)
    {
        questions.TryGetValue(column.SourceQuestion ?? column.Key, out var question);
        var panels = ReadParentPanels(question);
        if (panels.Count > 0 || column.LoopPath is not { Count: > 0 } loopPath)
        {
            return panels;
        }

        var loopName = loopPath[0].PanelValueName;
        var loopTitle = questions.TryGetValue(loopName, out var loop) ? ReadQuestionTitle(loop) : null;
        return [new ShojiPanel(loopName, string.IsNullOrWhiteSpace(loopTitle) ? loopName : loopTitle)];
    }

    private static List<ShojiPanel> ReadParentPanels(JsonElement question)
    {
        if (question.ValueKind != JsonValueKind.Object)
        {
            return [];
        }

        if (question.TryGetProperty(FormSchemaCodebookPropertyNames.ParentPanels, out var panels) &&
            panels.ValueKind == JsonValueKind.Array)
        {
            return [.. panels.EnumerateArray().Select(ReadPanel).OfType<ShojiPanel>()];
        }

        var title = ReadParentPanelTitle(question);
        return string.IsNullOrWhiteSpace(title) ? [] : [new ShojiPanel(title, title)];
    }

    // A blank title shows the name; a panel with neither leaves its questions in the folder around it.
    private static ShojiPanel? ReadPanel(JsonElement panel)
    {
        var name = panel.GetStringProperty(FormSchemaCodebookPropertyNames.PanelName);
        var title = panel.GetStringProperty(FormSchemaCodebookPropertyNames.PanelTitle);
        var label = string.IsNullOrWhiteSpace(title) ? name : title;
        if (string.IsNullOrWhiteSpace(label))
        {
            return null;
        }

        return new ShojiPanel(string.IsNullOrWhiteSpace(name) ? label : name, label);
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
