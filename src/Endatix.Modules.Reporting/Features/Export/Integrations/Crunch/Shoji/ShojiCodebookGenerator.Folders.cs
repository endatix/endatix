using System.Text.Json;
using System.Text.RegularExpressions;
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

    private sealed class ShojiFolder(string name) : IShojiOrderEntry
    {
        private readonly Dictionary<string, ShojiFolder> _subFoldersByPanel = new(StringComparer.Ordinal);

        // Crunch may compare folder names without case, so "Details" and "details" count as one name.
        private readonly HashSet<string> _subFolderNames = new(StringComparer.OrdinalIgnoreCase);

        public string Name { get; } = name;

        public List<IShojiOrderEntry> Entries { get; } = [];

        public static ShojiFolder Of(string name, IEnumerable<string> aliases)
        {
            ShojiFolder folder = new(name);
            folder.Entries.AddRange(aliases.Select(alias => new ShojiOrderAlias(alias)));
            return folder;
        }

        // A sub-folder takes its place where its first alias appears. Two panels with one title stay apart, the
        // second numbered ("Details (2)"), so no question moves out of survey order.
        public ShojiFolder SubFolder(ParentPanel panel)
        {
            if (!_subFoldersByPanel.TryGetValue(panel.Key, out var subFolder))
            {
                subFolder = new ShojiFolder(UniqueSubFolderName(FolderName(panel.Title)));
                _subFoldersByPanel.Add(panel.Key, subFolder);
                Entries.Add(subFolder);
            }

            return subFolder;
        }

        public ShojiFolder SubFolderAt(IEnumerable<ParentPanel> panels) =>
            panels.Aggregate(this, (parent, panel) => parent.SubFolder(panel));

        public void WriteTo(Utf8JsonWriter writer)
        {
            writer.WriteStartObject();
            writer.WriteString(ShojiCodebookPropertyNames.Group, Name);
            writer.WritePropertyName(ShojiCodebookPropertyNames.Entities);
            WriteOrderEntries(writer, Entries);
            writer.WriteEndObject();
        }

        // Numbered until no sibling has the name, so a panel titled "Details (2)" cannot meet a numbered repeat.
        private string UniqueSubFolderName(string folderName)
        {
            var unique = folderName;
            var repeat = 1;
            while (!_subFolderNames.Add(unique))
            {
                repeat++;
                unique = $"{folderName} ({repeat})";
            }

            return unique;
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

    /// <summary>
    /// Panel paths learned from the codebook's own <c>parentPanels</c>, to place a question whose entry has only the
    /// nearest panel's title (a codebook compiled before panel paths, or a stale entry a merge kept), and a loop
    /// column whose question the codebook lacks. A title two different panels share stays unresolved.
    /// </summary>
    private sealed class KnownPanelPaths(IReadOnlyDictionary<string, JsonElement> questions)
    {
        private readonly Dictionary<string, List<ParentPanel>?> _byTitle = new(StringComparer.Ordinal);
        private readonly Dictionary<string, List<ParentPanel>> _byLoop = new(StringComparer.Ordinal);

        public static KnownPanelPaths From(ShojiCodebookWriteArgs args)
        {
            KnownPanelPaths known = new(args.Questions);
            foreach (var question in args.Questions.Values)
            {
                known.AddTitles(ParentPanel.ReadAll(question));
            }

            foreach (var column in args.FlatteningMap.Columns.Where(column => column.LoopPath is { Count: > 0 }))
            {
                args.Questions.TryGetValue(column.SourceQuestion ?? column.Key, out var question);
                known.AddLoop(column.LoopPath![0].PanelValueName, ParentPanel.ReadAll(question));
            }

            return known;
        }

        public IReadOnlyDictionary<string, JsonElement> Questions { get; } = questions;

        // For a question entry with no panel list: its nearest panel's title, else its loop.
        public List<ParentPanel> ForUnlisted(FormSchemaColumn column, JsonElement question)
        {
            var title = ReadParentPanelTitle(question);
            if (!string.IsNullOrWhiteSpace(title))
            {
                return _byTitle.TryGetValue(title, out var path) && path is not null ? path : [new ParentPanel(title, title)];
            }

            if (column.LoopPath is not { Count: > 0 } loopPath)
            {
                return [];
            }

            var loopValueName = loopPath[0].PanelValueName;
            return _byLoop.GetValueOrDefault(loopValueName) ?? [LoopPanel(loopValueName)];
        }

        private ParentPanel LoopPanel(string loopValueName)
        {
            var loopTitle = Questions.TryGetValue(loopValueName, out var loop) ? ReadQuestionTitle(loop) : null;
            return ParentPanel.Of(loopValueName, loopTitle)!;
        }

        private void AddTitles(List<ParentPanel> panels)
        {
            for (var depth = 1; depth <= panels.Count; depth++)
            {
                var path = panels[..depth];
                var title = path[^1].Title;
                if (!_byTitle.TryGetValue(title, out var known))
                {
                    _byTitle[title] = path;
                }
                else if (known is not null && known[^1].Key != path[^1].Key)
                {
                    _byTitle[title] = null;
                }
            }
        }

        // The loop's own path is the shortest one its columns give: a panel inside the loop only lengthens it.
        private void AddLoop(string loopValueName, List<ParentPanel> panels)
        {
            if (panels.Count > 0 && (!_byLoop.TryGetValue(loopValueName, out var known) || panels.Count < known.Count))
            {
                _byLoop[loopValueName] = panels;
            }
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
        ShojiFolder questions = new(ShojiFolderNames.Questions);
        var known = KnownPanelPaths.From(sources.Args);
        foreach (var column in sources.Args.FlatteningMap.Columns)
        {
            var aliases = UnplacedAliases(column, sources, placed);
            if (aliases.Count > 0)
            {
                questions.SubFolderAt(PanelsOf(column, known)).Entries.AddRange(aliases);
            }
        }

        questions.Entries.AddRange(sources.WrittenAliases.Where(placed.Add).Select(alias => new ShojiOrderAlias(alias)));
        return questions;
    }

    private static List<ShojiOrderAlias> UnplacedAliases(FormSchemaColumn column, ShojiOrderSources sources, HashSet<string> placed) =>
        [.. ColumnOrderAliases(column, sources.Args)
            .Where(alias => sources.Written.Contains(alias) && placed.Add(alias))
            .Select(alias => new ShojiOrderAlias(alias))];

    // Folders take the codebook's panel titles, which are default-locale. An entry with only the nearest panel's
    // title, or a loop column with no question entry, takes a path the codebook's panel lists give.
    private static List<ParentPanel> PanelsOf(FormSchemaColumn column, KnownPanelPaths known)
    {
        known.Questions.TryGetValue(column.SourceQuestion ?? column.Key, out var question);
        var panels = ParentPanel.ReadAll(question);
        return panels.Count > 0 ? panels : known.ForUnlisted(column, question);
    }

    // A folder name is the panel title on one line: Crunch shows it as a label, and a line break or a run of spaces
    // would make two titles that read the same look different.
    private static string FolderName(string title) => Whitespace().Replace(title, " ").Trim();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();

    private static IEnumerable<string> ColumnOrderAliases(FormSchemaColumn column, ShojiCodebookWriteArgs args)
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
