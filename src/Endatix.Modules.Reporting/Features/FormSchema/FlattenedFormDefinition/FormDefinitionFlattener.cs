using System.Text.Json;
using Endatix.Modules.Reporting.Domain.SurveyJs;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;
using Endatix.Modules.Reporting.Shared.SurveyJs;

namespace Endatix.Modules.Reporting.Features.FormSchema.FlattenedFormDefinition;

/// <summary>
/// Walks a SurveyJS form definition and produces BI-ready form schema column definitions.
/// Mirrors semantics of <c>getPlainData</c> / nested-loop SQL export without a SurveyJS runtime.
/// </summary>
internal static class FormDefinitionFlattener
{
    private sealed record CollectedElement(
        JsonElement Element,
        int Depth,
        string? ParentValueName,
        string? Type,
        string? Name);

    private sealed record DynamicPanelNode(
        string PanelName,
        string ValueName,
        string ValuePropertyName,
        string? ParentValueName,
        JsonElement Choices,
        JsonElement[] TemplateElements);

    private sealed record PanelPath(
        IReadOnlyList<string> LoopPath,
        IReadOnlyList<string> PropertyPath,
        IReadOnlyList<JsonElement> ChoicesPath,
        JsonElement[] TemplateElements);

    private sealed record LoopSourceLeafPath(
        IReadOnlyList<string> LoopPath,
        IReadOnlyList<IReadOnlyList<string>> DriverChoicesByLevel,
        JsonElement[] TemplateElements);

    public static IReadOnlyList<FormSchemaColumn> Flatten(
        JsonElement definition,
        SchemaCompilationLimits? limits = null)
    {
        var effectiveLimits = limits ?? SchemaCompilationLimits.Default;
        var allElements = CollectElements(definition, effectiveLimits);
        EnforceMaxQuestions(allElements, definition, effectiveLimits);
        var drivingCheckboxNames = allElements
            .Where(item => IsDrivingCheckbox(item.Element, item.Type, item.Name))
            .Select(item => item.Name!)
            .ToHashSet(StringComparer.Ordinal);

        var rootElements = allElements
            .Where(item => item.Depth == 0)
            .ToList();

        var dynamicPanels = BuildDynamicPanelNodes(allElements, drivingCheckboxNames);
        var panelPaths = BuildPanelPaths(dynamicPanels, effectiveLimits);

        List<FormSchemaColumn> columns = [];
        HashSet<string> seenKeys = new(StringComparer.Ordinal);

        EmitRootElementColumns(rootElements, drivingCheckboxNames, columns, seenKeys, effectiveLimits);
        AddLoopSourceColumns(allElements, columns, seenKeys, effectiveLimits);
        AddNestedLoopColumns(panelPaths, drivingCheckboxNames, columns, seenKeys, effectiveLimits);
        AddCalculatedValueColumns(definition, columns, seenKeys, effectiveLimits);

        return columns;
    }

    private static List<CollectedElement> CollectElements(JsonElement definition, SchemaCompilationLimits limits)
    {
        List<CollectedElement> elements = [];
        CollectFromContainer(definition, elements, limits);
        return elements;
    }

    private static void EnforceMaxQuestions(
        IReadOnlyList<CollectedElement> allElements,
        JsonElement definition,
        SchemaCompilationLimits limits)
    {
        var questionCount = CountQuestions(allElements, definition);
        if (questionCount > limits.MaxQuestions)
        {
            ThrowLimitExceeded(
                SchemaCompilationLimitKind.MaxQuestions,
                limits.MaxQuestions,
                actual: questionCount);
        }
    }

    private static int CountQuestions(IReadOnlyList<CollectedElement> allElements, JsonElement definition)
    {
        var count = allElements.Count(item =>
            !SurveyJsElementType.IsNonData(item.Type) &&
            !SurveyJsElementType.IsContainer(item.Type));

        count += CountCalculatedValues(definition);
        return count;
    }

    private static int CountCalculatedValues(JsonElement definition)
    {
        if (!definition.TryGetCalculatedValues(out var calculatedValues))
        {
            return 0;
        }

        var count = 0;
        foreach (var calculatedValue in calculatedValues.EnumerateArray())
        {
            if (!calculatedValue.GetBooleanProperty(SurveyJsPropertyNames.IncludeIntoResult, defaultValue: true))
            {
                continue;
            }

            var name = calculatedValue.GetStringProperty(SurveyJsPropertyNames.Name);

            if (!string.IsNullOrWhiteSpace(name))
            {
                count++;
            }
        }

        return count;
    }

    private static void CollectFromContainer(
        JsonElement container,
        List<CollectedElement> elements,
        SchemaCompilationLimits limits)
    {
        if (container.TryGetPages(out var pages))
        {
            foreach (var page in pages.EnumerateArray())
            {
                if (page.TryGetElements(out var pageElements))
                {
                    CollectElementList(pageElements, depth: 0, parentValueName: null, elements, limits);
                }
            }
        }

        if (container.TryGetElements(out var rootElements))
        {
            CollectElementList(rootElements, depth: 0, parentValueName: null, elements, limits);
        }
    }

    private static void CollectElementList(
        JsonElement elementList,
        int depth,
        string? parentValueName,
        List<CollectedElement> elements,
        SchemaCompilationLimits limits)
    {
        if (depth > limits.MaxNestingDepth)
        {
            ThrowLimitExceeded(
                SchemaCompilationLimitKind.MaxNestingDepth,
                limits.MaxNestingDepth,
                actual: depth);
        }

        if (elementList.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var element in elementList.EnumerateArray())
        {
            var type = element.GetSurveyJsType();
            if (SurveyJsElementType.IsNonData(type))
            {
                continue;
            }

            var name = element.GetSurveyJsName();
            elements.Add(new CollectedElement(element, depth, parentValueName, type, name));

            CollectFromPanel(element, type, depth, parentValueName, elements, limits);
            CollectFromPage(element, type, depth, parentValueName, elements, limits);
            CollectFromPanelDynamic(element, type, depth, elements, limits);
        }
    }

    private static void CollectFromPanel(
        JsonElement element,
        string? type,
        int depth,
        string? parentValueName,
        List<CollectedElement> elements,
        SchemaCompilationLimits limits)
    {
        if (!SurveyJsElementType.Panel.Matches(type) ||
            !element.TryGetElements(out var panelChildren))
        {
            return;
        }

        CollectElementList(panelChildren, depth, parentValueName, elements, limits);
    }

    private static void CollectFromPage(
        JsonElement element,
        string? type,
        int depth,
        string? parentValueName,
        List<CollectedElement> elements,
        SchemaCompilationLimits limits)
    {
        if (!SurveyJsElementType.Page.Matches(type) ||
            !element.TryGetElements(out var pageChildren))
        {
            return;
        }

        CollectElementList(pageChildren, depth, parentValueName, elements, limits);
    }

    private static void CollectFromPanelDynamic(
        JsonElement element,
        string? type,
        int depth,
        List<CollectedElement> elements,
        SchemaCompilationLimits limits)
    {
        if (!SurveyJsElementType.PanelDynamic.Matches(type) ||
            !element.TryGetTemplateElements(out var templateElements))
        {
            return;
        }

        var panelValueName = element.GetSurveyJsValueName();

        CollectElementList(templateElements, depth + 1, panelValueName, elements, limits);
    }

    private static List<DynamicPanelNode> BuildDynamicPanelNodes(
        IReadOnlyList<CollectedElement> allElements,
        HashSet<string> drivingCheckboxNames)
    {
        Dictionary<string, CollectedElement> elementsByName = new(StringComparer.Ordinal);
        foreach (var collected in allElements)
        {
            if (!string.IsNullOrWhiteSpace(collected.Name))
            {
                elementsByName.TryAdd(collected.Name, collected);
            }
        }

        List<DynamicPanelNode> nodes = [];

        foreach (var collected in allElements)
        {
            if (!SurveyJsElementType.PanelDynamic.Matches(collected.Type))
            {
                continue;
            }

            var valueName = collected.Element.GetSurveyJsValueName();


            if (!TryResolveDynamicPanelDriver(valueName, elementsByName, drivingCheckboxNames, out _, out var valuePropertyName, out var choices))
            {
                continue;
            }

            var templateElements = collected.Element.TryGetTemplateElements(out var templateProp)
                ? templateProp.EnumerateArray().ToArray()
                : [];

            nodes.Add(new DynamicPanelNode(
                collected.Name ?? valueName!,
                valueName!,
                valuePropertyName,
                collected.ParentValueName,
                choices,
                templateElements));
        }

        return nodes;
    }

    private static bool TryResolveDynamicPanelDriver(
        string? valueName,
        Dictionary<string, CollectedElement> elementsByName,
        HashSet<string> drivingCheckboxNames,
        out CollectedElement driver,
        out string valuePropertyName,
        out JsonElement choices)
    {
        driver = default!;
        valuePropertyName = string.Empty;
        choices = default;

        if (string.IsNullOrWhiteSpace(valueName) || !drivingCheckboxNames.Contains(valueName))
        {
            return false;
        }

        if (!elementsByName.TryGetValue(valueName!, out var resolvedDriver))
        {
            return false;
        }

        driver = resolvedDriver;

        valuePropertyName = resolvedDriver.Element.GetStringProperty(SurveyJsPropertyNames.ValuePropertyName) ?? string.Empty;
        if (string.IsNullOrWhiteSpace(valuePropertyName))
        {
            return false;
        }

        if (!driver.Element.TryGetChoices(out choices))
        {
            choices = default;
        }

        return true;
    }

    private static List<PanelPath> BuildPanelPaths(
        IReadOnlyList<DynamicPanelNode> dynamicPanels,
        SchemaCompilationLimits limits)
    {
        var panelsByValueName = dynamicPanels.ToDictionary(
            panel => panel.ValueName,
            StringComparer.Ordinal);

        List<PanelPath> paths = [];
        var roots = dynamicPanels.Where(panel => panel.ParentValueName is null);

        foreach (var root in roots)
        {
            BuildPanelPathsRecursive(
                root,
                loopPath: [],
                propertyPath: [],
                choicesPath: [],
                panelsByValueName,
                paths,
                limits);
        }

        return paths;
    }

    private static void BuildPanelPathsRecursive(
        DynamicPanelNode panel,
        IReadOnlyList<string> loopPath,
        IReadOnlyList<string> propertyPath,
        IReadOnlyList<JsonElement> choicesPath,
        IReadOnlyDictionary<string, DynamicPanelNode> panelsByValueName,
        List<PanelPath> paths,
        SchemaCompilationLimits limits)
    {
        if (loopPath.Count >= limits.MaxNestingDepth)
        {
            ThrowLimitExceeded(
                SchemaCompilationLimitKind.MaxNestingDepth,
                limits.MaxNestingDepth,
                actual: loopPath.Count + 1,
                context: panel.ValueName);
        }

        List<string> nextLoopPath = [.. loopPath, panel.ValueName];
        List<string> nextPropertyPath = [.. propertyPath, panel.ValuePropertyName];
        List<JsonElement> nextChoicesPath = [.. choicesPath, panel.Choices];

        var childPanel = panel.TemplateElements
            .Where(template => SurveyJsElementType.PanelDynamic.Matches(template.GetSurveyJsType()))
            .Select(template =>
            {
                var childValueName = template.GetSurveyJsValueName();

                if (childValueName is not null && panelsByValueName.TryGetValue(childValueName, out var child))
                {
                    return child;
                }

                return null;
            })
            .FirstOrDefault(match => match is not null);

        if (childPanel is not null &&
            string.Equals(childPanel.ParentValueName, panel.ValueName, StringComparison.Ordinal))
        {
            BuildPanelPathsRecursive(
                childPanel,
                nextLoopPath,
                nextPropertyPath,
                nextChoicesPath,
                panelsByValueName,
                paths,
                limits);
            return;
        }

        paths.Add(new PanelPath(nextLoopPath, nextPropertyPath, nextChoicesPath, panel.TemplateElements));
    }

    private static void AddCalculatedValueColumns(
        JsonElement definition,
        List<FormSchemaColumn> columns,
        HashSet<string> seenKeys,
        SchemaCompilationLimits limits)
    {
        if (!definition.TryGetCalculatedValues(out var calculatedValues))
        {
            return;
        }

        foreach (var calculatedValue in calculatedValues.EnumerateArray())
        {
            if (!calculatedValue.GetBooleanProperty(SurveyJsPropertyNames.IncludeIntoResult, defaultValue: true))
            {
                continue;
            }

            var name = calculatedValue.GetStringProperty(SurveyJsPropertyNames.Name);

            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            AddColumn(columns, seenKeys, limits, new FormSchemaColumn(
                name,
                FormSchemaColumnKind.Calculated,
                name,
                "string"));
        }
    }

    private static void EmitRootElementColumns(
        IReadOnlyList<CollectedElement> rootElements,
        HashSet<string> drivingCheckboxNames,
        List<FormSchemaColumn> columns,
        HashSet<string> seenKeys,
        SchemaCompilationLimits limits)
    {
        foreach (var collected in rootElements)
        {
            if (string.IsNullOrWhiteSpace(collected.Name) ||
                SurveyJsElementType.IsNonData(collected.Type))
            {
                continue;
            }

            var flattening = SurveyJsElementType.ResolveFlattening(collected.Type, collected.Element);

            switch (flattening)
            {
                case SurveyJsFlattening.None:
                    break;
                case SurveyJsFlattening.ChoiceIndicators:
                    EmitChoiceIndicatorColumns(collected, drivingCheckboxNames, columns, seenKeys, limits);
                    break;
                case SurveyJsFlattening.Ranking:
                    EmitRankingColumns(collected, columns, seenKeys, limits);
                    break;
                case SurveyJsFlattening.Matrix:
                    EmitMatrixColumns(collected, columns, seenKeys, limits);
                    break;
                case SurveyJsFlattening.MultipleText:
                    EmitMultipleTextColumns(collected, columns, seenKeys, limits);
                    break;
                case SurveyJsFlattening.File:
                    EmitFileColumn(collected, columns, seenKeys, limits);
                    break;
                case SurveyJsFlattening.PanelDynamic:
                    if (!HasLoopSource(collected.Element))
                    {
                        EmitStandalonePanelDynamicColumns(collected, drivingCheckboxNames, columns, seenKeys, limits);
                    }

                    break;
                case SurveyJsFlattening.Simple:
                    EmitSimpleColumn(collected, drivingCheckboxNames, columns, seenKeys, limits);
                    break;
            }
        }
    }

    private static void EmitSimpleColumn(
        CollectedElement collected,
        HashSet<string> drivingCheckboxNames,
        List<FormSchemaColumn> columns,
        HashSet<string> seenKeys,
        SchemaCompilationLimits limits)
    {
        var type = collected.Type;
        var name = collected.Name!;

        if (SurveyJsElementType.IsContainer(type) ||
            drivingCheckboxNames.Contains(name))
        {
            return;
        }

        AddColumn(columns, seenKeys, limits, new FormSchemaColumn(
            name,
            FormSchemaColumnKind.Simple,
            collected.Element.GetSurveyJsTitle(name),
            MapDataType(collected.Element, type)));
    }

    private static void EmitChoiceIndicatorColumns(
        CollectedElement collected,
        HashSet<string> drivingCheckboxNames,
        List<FormSchemaColumn> columns,
        HashSet<string> seenKeys,
        SchemaCompilationLimits limits)
    {
        var name = collected.Name!;
        var type = collected.Type;

        if (drivingCheckboxNames.Contains(name))
        {
            return;
        }

        if (SurveyJsElementType.Boolean.Matches(type))
        {
            EmitBooleanChoiceIndicators(collected, columns, seenKeys, limits);
            return;
        }

        var choiceCount = 0;
        foreach ((var value, var text) in SurveyJsChoiceHelper.EnumerateChoices(collected.Element))
        {
            if (++choiceCount > limits.MaxChoicesPerQuestion)
            {
                ThrowLimitExceeded(
                    SchemaCompilationLimitKind.MaxChoicesPerQuestion,
                    limits.MaxChoicesPerQuestion,
                    actual: choiceCount,
                    context: name);
            }

            AddChoiceIndicatorColumn(collected, name, value, text, columns, seenKeys, limits);
        }

        if (collected.Element.GetBooleanProperty(SurveyJsPropertyNames.ShowOtherItem))
        {
            var otherLabel = collected.Element.GetStringProperty(SurveyJsPropertyNames.OtherText) ?? "Other";
            AddChoiceIndicatorColumn(
                collected,
                name,
                SurveyJsPropertyNames.Other,
                otherLabel,
                columns,
                seenKeys,
                limits);
            AddColumn(columns, seenKeys, limits, new FormSchemaColumn(
                ExportPathBuilder.ChoiceOtherTextKey(name),
                FormSchemaColumnKind.CheckboxOtherText,
                $"{collected.Element.GetSurveyJsTitle(name)} — Other text",
                "string",
                SourceQuestion: name));
        }
    }

    private static void EmitBooleanChoiceIndicators(
        CollectedElement collected,
        List<FormSchemaColumn> columns,
        HashSet<string> seenKeys,
        SchemaCompilationLimits limits)
    {
        var name = collected.Name!;
        var title = collected.Element.GetSurveyJsTitle(name);
        var trueLabel = collected.Element.GetStringProperty(SurveyJsPropertyNames.LabelTrue) ?? "Yes";
        var falseLabel = collected.Element.GetStringProperty(SurveyJsPropertyNames.LabelFalse) ?? "No";

        AddChoiceIndicatorColumn(collected, name, "true", trueLabel, columns, seenKeys, limits);
        AddChoiceIndicatorColumn(collected, name, "false", falseLabel, columns, seenKeys, limits);
    }

    private static void AddChoiceIndicatorColumn(
        CollectedElement collected,
        string name,
        string choiceValue,
        string choiceLabel,
        List<FormSchemaColumn> columns,
        HashSet<string> seenKeys,
        SchemaCompilationLimits limits)
    {
        var key = ExportPathBuilder.ChoiceIndicatorKey(name, choiceValue);
        AddColumn(columns, seenKeys, limits, new FormSchemaColumn(
            key,
            FormSchemaColumnKind.ChoiceIndicator,
            $"{collected.Element.GetSurveyJsTitle(name)} — {choiceLabel}",
            "number",
            SourceQuestion: name,
            ChoiceValue: choiceValue));
    }

    private static void EmitRankingColumns(
        CollectedElement collected,
        List<FormSchemaColumn> columns,
        HashSet<string> seenKeys,
        SchemaCompilationLimits limits)
    {
        var name = collected.Name!;

        var choiceCount = 0;
        foreach ((var value, var text) in SurveyJsChoiceHelper.EnumerateChoices(collected.Element))
        {
            if (++choiceCount > limits.MaxChoicesPerQuestion)
            {
                ThrowLimitExceeded(
                    SchemaCompilationLimitKind.MaxChoicesPerQuestion,
                    limits.MaxChoicesPerQuestion,
                    actual: choiceCount,
                    context: name);
            }

            var key = ExportPathBuilder.RankingChoiceKey(name, value);
            AddColumn(columns, seenKeys, limits, new FormSchemaColumn(
                key,
                FormSchemaColumnKind.RankingChoice,
                $"{collected.Element.GetSurveyJsTitle(name)} — {text}",
                "number",
                SourceQuestion: name,
                ChoiceValue: value));
        }
    }

    private static void EmitMatrixColumns(
        CollectedElement collected,
        List<FormSchemaColumn> columns,
        HashSet<string> seenKeys,
        SchemaCompilationLimits limits)
    {
        if (SurveyJsElementType.MatrixDropdown.Matches(collected.Type))
        {
            EmitMatrixDropdownColumns(collected, columns, seenKeys, limits);
            return;
        }

        if (SurveyJsElementType.MatrixDynamic.Matches(collected.Type))
        {
            EmitMatrixDynamicColumns(collected, columns, seenKeys, limits);
            return;
        }

        EmitPlainMatrixColumns(collected, columns, seenKeys, limits);
    }

    private static void EmitPlainMatrixColumns(
        CollectedElement collected,
        List<FormSchemaColumn> columns,
        HashSet<string> seenKeys,
        SchemaCompilationLimits limits)
    {
        var name = collected.Name!;
        IReadOnlyList<string> columnChoices = CollectMatrixColumnChoices(collected.Element);

        var rowCount = 0;
        foreach ((var value, var text) in SurveyJsChoiceHelper.EnumerateMatrixRows(collected.Element))
        {
            if (++rowCount > limits.MaxChoicesPerQuestion)
            {
                ThrowLimitExceeded(
                    SchemaCompilationLimitKind.MaxChoicesPerQuestion,
                    limits.MaxChoicesPerQuestion,
                    actual: rowCount,
                    context: name);
            }

            var key = ExportPathBuilder.MatrixRowKey(name, value);
            AddColumn(columns, seenKeys, limits, new FormSchemaColumn(
                key,
                FormSchemaColumnKind.MatrixRow,
                $"{collected.Element.GetSurveyJsTitle(name)} — {text}",
                "number",
                SourceQuestion: name,
                MatrixRowValue: value,
                MatrixColumnChoices: columnChoices));
        }
    }

    private static IReadOnlyList<string> CollectMatrixColumnChoices(JsonElement matrixElement)
    {
        List<string> columnChoices = [];
        foreach ((var value, _, _) in SurveyJsChoiceHelper.EnumerateMatrixColumns(matrixElement))
        {
            columnChoices.Add(value);
        }

        return columnChoices;
    }

    private static void EmitMatrixDropdownColumns(
        CollectedElement collected,
        List<FormSchemaColumn> columns,
        HashSet<string> seenKeys,
        SchemaCompilationLimits limits)
    {
        var name = collected.Name!;
        var cellCount = 0;

        foreach ((var rowValue, var rowText) in SurveyJsChoiceHelper.EnumerateMatrixRows(collected.Element))
        {
            foreach ((var columnValue, var columnText, var columnElement) in SurveyJsChoiceHelper.EnumerateMatrixColumns(collected.Element))
            {
                if (++cellCount > limits.MaxChoicesPerQuestion)
                {
                    ThrowLimitExceeded(
                        SchemaCompilationLimitKind.MaxChoicesPerQuestion,
                        limits.MaxChoicesPerQuestion,
                        actual: cellCount,
                        context: name);
                }

                var key = ExportPathBuilder.MatrixCellKey(name, rowValue, columnValue);
                AddColumn(columns, seenKeys, limits, new FormSchemaColumn(
                    key,
                    FormSchemaColumnKind.MatrixCell,
                    $"{collected.Element.GetSurveyJsTitle(name)} — {rowText} — {columnText}",
                    MapMatrixCellDataType(columnElement),
                    SourceQuestion: name,
                    MatrixRowValue: rowValue,
                    MatrixColumnValue: columnValue));
            }
        }
    }

    private static void EmitMatrixDynamicColumns(
        CollectedElement collected,
        List<FormSchemaColumn> columns,
        HashSet<string> seenKeys,
        SchemaCompilationLimits limits)
    {
        var name = collected.Name!;
        var rowCount = ResolveMatrixRowCount(collected.Element, limits);
        var cellCount = 0;

        for (var rowIndex = 0; rowIndex < rowCount; rowIndex++)
        {
            var rowSegment = rowIndex.ToString(System.Globalization.CultureInfo.InvariantCulture);

            foreach ((var columnValue, var columnText, var columnElement) in SurveyJsChoiceHelper.EnumerateMatrixColumns(collected.Element))
            {
                if (++cellCount > limits.MaxChoicesPerQuestion)
                {
                    ThrowLimitExceeded(
                        SchemaCompilationLimitKind.MaxChoicesPerQuestion,
                        limits.MaxChoicesPerQuestion,
                        actual: cellCount,
                        context: name);
                }

                var key = ExportPathBuilder.MatrixCellKey(name, rowSegment, columnValue);
                AddColumn(columns, seenKeys, limits, new FormSchemaColumn(
                    key,
                    FormSchemaColumnKind.MatrixCell,
                    $"{collected.Element.GetSurveyJsTitle(name)} — #{rowIndex + 1} — {columnText}",
                    MapMatrixCellDataType(columnElement),
                    SourceQuestion: name,
                    PanelIndex: rowIndex,
                    MatrixColumnValue: columnValue));
            }
        }
    }

    private static int ResolveMatrixRowCount(JsonElement matrixElement, SchemaCompilationLimits limits)
    {
        int configuredCount;
        if (matrixElement.TryGetInt32Property(SurveyJsPropertyNames.MaxRowCount, out var maxRowCount))
        {
            configuredCount = maxRowCount;
        }
        else if (matrixElement.TryGetInt32Property(SurveyJsPropertyNames.RowCount, out var rowCount))
        {
            configuredCount = rowCount;
        }
        else
        {
            configuredCount = limits.MaxMatrixRowCount;
        }

        if (configuredCount < 0)
        {
            configuredCount = 0;
        }

        return Math.Min(configuredCount, limits.MaxMatrixRowCount);
    }

    private static string MapMatrixCellDataType(JsonElement columnElement)
    {
        if (columnElement.ValueKind != JsonValueKind.Object)
        {
            return "string";
        }

        if (string.Equals(
                columnElement.GetStringProperty(SurveyJsPropertyNames.InputType),
                "number",
                StringComparison.OrdinalIgnoreCase))
        {
            return "number";
        }

        return "string";
    }

    private static void EmitMultipleTextColumns(
        CollectedElement collected,
        List<FormSchemaColumn> columns,
        HashSet<string> seenKeys,
        SchemaCompilationLimits limits)
    {
        var name = collected.Name!;

        var itemCount = 0;
        foreach ((var value, var text) in SurveyJsChoiceHelper.EnumerateMultipleTextItems(collected.Element))
        {
            if (++itemCount > limits.MaxChoicesPerQuestion)
            {
                ThrowLimitExceeded(
                    SchemaCompilationLimitKind.MaxChoicesPerQuestion,
                    limits.MaxChoicesPerQuestion,
                    actual: itemCount,
                    context: name);
            }

            var key = ExportPathBuilder.MultipleTextItemKey(name, value);
            AddColumn(columns, seenKeys, limits, new FormSchemaColumn(
                key,
                FormSchemaColumnKind.MultipleTextItem,
                $"{collected.Element.GetSurveyJsTitle(name)} — {text}",
                "string",
                SourceQuestion: name,
                MatrixRowValue: value));
        }
    }

    private static void EmitFileColumn(
        CollectedElement collected,
        List<FormSchemaColumn> columns,
        HashSet<string> seenKeys,
        SchemaCompilationLimits limits)
    {
        var name = collected.Name!;

        AddColumn(columns, seenKeys, limits, new FormSchemaColumn(
            name,
            FormSchemaColumnKind.FileUpload,
            collected.Element.GetSurveyJsTitle(name),
            "file"));
    }

    private static void EmitStandalonePanelDynamicColumns(
        CollectedElement collected,
        HashSet<string> drivingCheckboxNames,
        List<FormSchemaColumn> columns,
        HashSet<string> seenKeys,
        SchemaCompilationLimits limits)
    {
        if (HasLoopSource(collected.Element))
        {
            return;
        }

        var name = collected.Name!;

        var valueName = collected.Element.GetSurveyJsValueName();
        if (!string.IsNullOrWhiteSpace(valueName) &&
            drivingCheckboxNames.Contains(valueName))
        {
            return;
        }

        if (!collected.Element.TryGetTemplateElements(out var templateElements))
        {
            return;
        }

        var maxPanelCount = ResolvePanelCount(collected.Element, limits);

        for (var index = 0; index < maxPanelCount; index++)
        {
            foreach (var template in templateElements.EnumerateArray())
            {
                var childType = template.GetSurveyJsType();
                var childName = template.GetSurveyJsName();

                if (string.IsNullOrWhiteSpace(childName) ||
                    SurveyJsElementType.IsNonData(childType) ||
                    SurveyJsElementType.PanelDynamic.Matches(childType))
                {
                    continue;
                }

                var key = ExportPathBuilder.PanelIndexKey(name, index, childName);
                AddColumn(columns, seenKeys, limits, new FormSchemaColumn(
                    key,
                    FormSchemaColumnKind.PanelDynamicIndex,
                    $"{template.GetSurveyJsTitle(childName)} ({name} #{index + 1})",
                    MapDataType(template, childType),
                    SourceQuestion: childName,
                    PanelName: name,
                    PanelIndex: index));
            }
        }
    }

    private static int ResolvePanelCount(JsonElement panelElement, SchemaCompilationLimits limits)
    {
        var configuredCount = panelElement.TryGetInt32Property(SurveyJsPropertyNames.MaxPanelCount, out var maxPanelCount)
            ? maxPanelCount
            : limits.MaxPanelCount;

        if (configuredCount < 0)
        {
            configuredCount = 0;
        }

        return Math.Min(configuredCount, limits.MaxPanelCount);
    }

    private static bool HasLoopSource(JsonElement element) =>
        element.TryGetLoopSource(out var loopSource) && loopSource.GetArrayLength() > 0;

    private static void AddLoopSourceColumns(
        IReadOnlyList<CollectedElement> allElements,
        List<FormSchemaColumn> columns,
        HashSet<string> seenKeys,
        SchemaCompilationLimits limits)
    {
        Dictionary<string, CollectedElement> elementsByName = new(StringComparer.Ordinal);
        foreach (var collected in allElements)
        {
            if (!string.IsNullOrWhiteSpace(collected.Name))
            {
                elementsByName.TryAdd(collected.Name, collected);
            }
        }

        Dictionary<string, CollectedElement> loopPanelsByName = new(StringComparer.Ordinal);
        foreach (var collected in allElements)
        {
            if (!SurveyJsElementType.PanelDynamic.Matches(collected.Type) ||
                !HasLoopSource(collected.Element) ||
                string.IsNullOrWhiteSpace(collected.Name))
            {
                continue;
            }

            loopPanelsByName.TryAdd(collected.Name, collected);
        }

        if (loopPanelsByName.Count == 0)
        {
            return;
        }

        HashSet<string> childLoopPanels = new(StringComparer.Ordinal);
        foreach (var panel in loopPanelsByName.Values)
        {
            if (!panel.Element.TryGetTemplateElements(out var templateElements))
            {
                continue;
            }

            foreach (var template in templateElements.EnumerateArray())
            {
                var templateName = template.GetSurveyJsName();
                if (templateName is not null && loopPanelsByName.ContainsKey(templateName))
                {
                    childLoopPanels.Add(templateName);
                }
            }
        }

        List<LoopSourceLeafPath> leafPaths = [];
        foreach (var panel in loopPanelsByName.Values)
        {
            if (childLoopPanels.Contains(panel.Name!))
            {
                continue;
            }

            BuildLoopSourceLeafPaths(
                panel,
                loopPath: [],
                driverChoicesByLevel: [],
                loopPanelsByName,
                elementsByName,
                leafPaths,
                limits);
        }

        foreach (var leafPath in leafPaths)
        {
            foreach (var driverChoices in EnumerateLoopSourceDriverCombinations(leafPath.DriverChoicesByLevel, limits))
            {
                EmitLoopSourceColumnsForPath(leafPath, driverChoices, columns, seenKeys, limits);
            }
        }
    }

    private static void BuildLoopSourceLeafPaths(
        CollectedElement panel,
        IReadOnlyList<string> loopPath,
        IReadOnlyList<IReadOnlyList<string>> driverChoicesByLevel,
        IReadOnlyDictionary<string, CollectedElement> loopPanelsByName,
        IReadOnlyDictionary<string, CollectedElement> elementsByName,
        List<LoopSourceLeafPath> leafPaths,
        SchemaCompilationLimits limits)
    {
        var panelName = panel.Name!;
        List<string> nextLoopPath = [.. loopPath, panelName];
        List<IReadOnlyList<string>> nextDriverChoicesByLevel =
            [.. driverChoicesByLevel, CollectLoopSourceDriverChoices(panel.Element, elementsByName, limits)];

        if (!panel.Element.TryGetTemplateElements(out var templateElements))
        {
            return;
        }

        CollectedElement? childLoopPanel = null;
        foreach (var template in templateElements.EnumerateArray())
        {
            var templateName = template.GetSurveyJsName();
            if (templateName is not null &&
                loopPanelsByName.TryGetValue(templateName, out var childPanel))
            {
                childLoopPanel = childPanel;
                break;
            }
        }

        if (childLoopPanel is not null)
        {
            BuildLoopSourceLeafPaths(
                childLoopPanel,
                nextLoopPath,
                nextDriverChoicesByLevel,
                loopPanelsByName,
                elementsByName,
                leafPaths,
                limits);
            return;
        }

        JsonElement[] templateArray = templateElements.EnumerateArray()
            .Where(template =>
                !SurveyJsElementType.IsNonData(template.GetSurveyJsType()) &&
                !SurveyJsElementType.PanelDynamic.Matches(template.GetSurveyJsType()))
            .ToArray();

        leafPaths.Add(new LoopSourceLeafPath(nextLoopPath, nextDriverChoicesByLevel, templateArray));
    }

    private static IReadOnlyList<string> CollectLoopSourceDriverChoices(
        JsonElement panelElement,
        IReadOnlyDictionary<string, CollectedElement> elementsByName,
        SchemaCompilationLimits limits)
    {
        if (!panelElement.TryGetLoopSource(out var loopSource))
        {
            return [];
        }

        HashSet<string> seenChoices = new(StringComparer.Ordinal);
        List<string> driverChoices = [];

        foreach (var sourceNameElement in loopSource.EnumerateArray())
        {
            if (sourceNameElement.ValueKind != JsonValueKind.String)
            {
                continue;
            }

            var sourceName = sourceNameElement.GetString();
            if (string.IsNullOrWhiteSpace(sourceName) ||
                !elementsByName.TryGetValue(sourceName, out var sourceElement))
            {
                continue;
            }

            var choiceCount = 0;
            foreach (var choiceValue in SurveyJsChoiceHelper.EnumerateLoopSourceDriverChoices(sourceElement.Element))
            {
                if (++choiceCount > limits.MaxChoicesPerQuestion)
                {
                    ThrowLimitExceeded(
                        SchemaCompilationLimitKind.MaxChoicesPerQuestion,
                        limits.MaxChoicesPerQuestion,
                        actual: choiceCount,
                        context: sourceName);
                }

                if (seenChoices.Add(choiceValue))
                {
                    driverChoices.Add(choiceValue);
                }
            }
        }

        return driverChoices;
    }

    private static IEnumerable<string[]> EnumerateLoopSourceDriverCombinations(
        IReadOnlyList<IReadOnlyList<string>> driverChoicesByLevel,
        SchemaCompilationLimits limits)
    {
        if (driverChoicesByLevel.Count == 0)
        {
            yield break;
        }

        List<IReadOnlyList<string>> levels = [.. driverChoicesByLevel];
        List<int> levelSizes = levels.Select(level => level.Count).ToList();

        var combinationCount = ChoiceCartesianProduct.EstimateCombinationCount(levelSizes);
        if (combinationCount == 0)
        {
            yield break;
        }

        if (combinationCount > limits.MaxLoopCombinations)
        {
            ThrowLimitExceeded(
                SchemaCompilationLimitKind.MaxLoopCombinations,
                limits.MaxLoopCombinations,
                actual: combinationCount > int.MaxValue ? int.MaxValue : (int)combinationCount);
        }

        foreach (var combination in ChoiceCartesianProduct.Enumerate(levels))
        {
            yield return combination;
        }
    }

    private static void EmitLoopSourceColumnsForPath(
        LoopSourceLeafPath leafPath,
        IReadOnlyList<string> driverChoices,
        List<FormSchemaColumn> columns,
        HashSet<string> seenKeys,
        SchemaCompilationLimits limits)
    {
        if (leafPath.LoopPath.Count == 0 || driverChoices.Count != leafPath.LoopPath.Count)
        {
            return;
        }

        var keyPanelName = leafPath.LoopPath[^1];
        List<LoopSegment> loopSegments = new(leafPath.LoopPath.Count);
        for (var i = 0; i < leafPath.LoopPath.Count; i++)
        {
            loopSegments.Add(new LoopSegment(
                leafPath.LoopPath[i],
                SurveyJsPropertyNames.ItemValue,
                driverChoices[i]));
        }

        foreach (var template in leafPath.TemplateElements)
        {
            EmitLoopSourceTemplateColumns(
                keyPanelName,
                driverChoices,
                loopSegments,
                template,
                columns,
                seenKeys,
                limits);
        }
    }

    private static void EmitLoopSourceTemplateColumns(
        string keyPanelName,
        IReadOnlyList<string> driverChoices,
        IReadOnlyList<LoopSegment> loopPath,
        JsonElement template,
        List<FormSchemaColumn> columns,
        HashSet<string> seenKeys,
        SchemaCompilationLimits limits)
    {
        var childName = template.GetSurveyJsName();
        var childType = template.GetSurveyJsType();

        if (string.IsNullOrWhiteSpace(childName) ||
            SurveyJsElementType.IsNonData(childType) ||
            SurveyJsElementType.PanelDynamic.Matches(childType))
        {
            return;
        }

        List<string> keyPrefix = [keyPanelName, .. driverChoices];
        var flattening = SurveyJsElementType.ResolveFlattening(childType, template);

        switch (flattening)
        {
            case SurveyJsFlattening.ChoiceIndicators:
                EmitLoopSourceChoiceIndicatorColumns(
                    template,
                    childName,
                    childType,
                    keyPrefix,
                    loopPath,
                    columns,
                    seenKeys,
                    limits);
                break;
            case SurveyJsFlattening.Simple:
                AddColumn(columns, seenKeys, limits, new FormSchemaColumn(
                    ExportPathBuilder.Join([.. keyPrefix, childName]),
                    FormSchemaColumnKind.LoopSource,
                    template.GetSurveyJsTitle(childName),
                    MapDataType(template, childType),
                    SourceQuestion: childName,
                    LoopPath: loopPath));
                break;
            case SurveyJsFlattening.Ranking:
                EmitLoopSourceRankingColumns(template, childName, keyPrefix, loopPath, columns, seenKeys, limits);
                break;
            case SurveyJsFlattening.Matrix:
                EmitLoopSourceMatrixColumns(template, childName, childType, keyPrefix, loopPath, columns, seenKeys, limits);
                break;
            case SurveyJsFlattening.MultipleText:
                EmitLoopSourceMultipleTextColumns(template, childName, keyPrefix, loopPath, columns, seenKeys, limits);
                break;
            case SurveyJsFlattening.File:
                AddColumn(columns, seenKeys, limits, new FormSchemaColumn(
                    ExportPathBuilder.Join([.. keyPrefix, childName]),
                    FormSchemaColumnKind.LoopSource,
                    template.GetSurveyJsTitle(childName),
                    "file",
                    SourceQuestion: childName,
                    LoopPath: loopPath));
                break;
        }
    }

    private static void EmitLoopSourceChoiceIndicatorColumns(
        JsonElement template,
        string childName,
        string? childType,
        IReadOnlyList<string> keyPrefix,
        IReadOnlyList<LoopSegment> loopPath,
        List<FormSchemaColumn> columns,
        HashSet<string> seenKeys,
        SchemaCompilationLimits limits)
    {
        if (SurveyJsElementType.Boolean.Matches(childType))
        {
            var title = template.GetSurveyJsTitle(childName);
            var trueLabel = template.GetStringProperty(SurveyJsPropertyNames.LabelTrue) ?? "Yes";
            var falseLabel = template.GetStringProperty(SurveyJsPropertyNames.LabelFalse) ?? "No";

            AddLoopSourceChoiceIndicatorColumn(
                template, childName, "true", trueLabel, keyPrefix, loopPath, columns, seenKeys, limits);
            AddLoopSourceChoiceIndicatorColumn(
                template, childName, "false", falseLabel, keyPrefix, loopPath, columns, seenKeys, limits);
            return;
        }

        var choiceCount = 0;
        foreach ((var value, var text) in SurveyJsChoiceHelper.EnumerateChoices(template))
        {
            if (++choiceCount > limits.MaxChoicesPerQuestion)
            {
                ThrowLimitExceeded(
                    SchemaCompilationLimitKind.MaxChoicesPerQuestion,
                    limits.MaxChoicesPerQuestion,
                    actual: choiceCount,
                    context: childName);
            }

            AddLoopSourceChoiceIndicatorColumn(
                template, childName, value, text, keyPrefix, loopPath, columns, seenKeys, limits);
        }

        if (template.GetBooleanProperty(SurveyJsPropertyNames.ShowOtherItem))
        {
            var otherLabel = template.GetStringProperty(SurveyJsPropertyNames.OtherText) ?? "Other";
            AddLoopSourceChoiceIndicatorColumn(
                template,
                childName,
                SurveyJsPropertyNames.Other,
                otherLabel,
                keyPrefix,
                loopPath,
                columns,
                seenKeys,
                limits);
            AddColumn(columns, seenKeys, limits, new FormSchemaColumn(
                ExportPathBuilder.Join([.. keyPrefix, childName, "other_text"]),
                FormSchemaColumnKind.CheckboxOtherText,
                $"{template.GetSurveyJsTitle(childName)} — Other text",
                "string",
                SourceQuestion: childName,
                LoopPath: loopPath));
        }
    }

    private static void AddLoopSourceChoiceIndicatorColumn(
        JsonElement template,
        string childName,
        string choiceValue,
        string choiceLabel,
        IReadOnlyList<string> keyPrefix,
        IReadOnlyList<LoopSegment> loopPath,
        List<FormSchemaColumn> columns,
        HashSet<string> seenKeys,
        SchemaCompilationLimits limits)
    {
        AddColumn(columns, seenKeys, limits, new FormSchemaColumn(
            ExportPathBuilder.Join([.. keyPrefix, childName, choiceValue]),
            FormSchemaColumnKind.ChoiceIndicator,
            $"{template.GetSurveyJsTitle(childName)} — {choiceLabel}",
            "number",
            SourceQuestion: childName,
            ChoiceValue: choiceValue,
            LoopPath: loopPath));
    }

    private static void EmitLoopSourceRankingColumns(
        JsonElement template,
        string childName,
        IReadOnlyList<string> keyPrefix,
        IReadOnlyList<LoopSegment> loopPath,
        List<FormSchemaColumn> columns,
        HashSet<string> seenKeys,
        SchemaCompilationLimits limits)
    {
        var choiceCount = 0;
        foreach ((var value, var text) in SurveyJsChoiceHelper.EnumerateChoices(template))
        {
            if (++choiceCount > limits.MaxChoicesPerQuestion)
            {
                ThrowLimitExceeded(
                    SchemaCompilationLimitKind.MaxChoicesPerQuestion,
                    limits.MaxChoicesPerQuestion,
                    actual: choiceCount,
                    context: childName);
            }

            AddColumn(columns, seenKeys, limits, new FormSchemaColumn(
                ExportPathBuilder.Join([.. keyPrefix, childName, value]),
                FormSchemaColumnKind.RankingChoice,
                $"{template.GetSurveyJsTitle(childName)} — {text}",
                "number",
                SourceQuestion: childName,
                ChoiceValue: value,
                LoopPath: loopPath));
        }
    }

    private static void EmitLoopSourceMatrixColumns(
        JsonElement template,
        string childName,
        string? childType,
        IReadOnlyList<string> keyPrefix,
        IReadOnlyList<LoopSegment> loopPath,
        List<FormSchemaColumn> columns,
        HashSet<string> seenKeys,
        SchemaCompilationLimits limits)
    {
        if (SurveyJsElementType.MatrixDropdown.Matches(childType))
        {
            foreach ((var rowValue, var rowText) in SurveyJsChoiceHelper.EnumerateMatrixRows(template))
            {
                foreach ((var columnValue, var columnText, var columnElement) in SurveyJsChoiceHelper.EnumerateMatrixColumns(template))
                {
                    AddColumn(columns, seenKeys, limits, new FormSchemaColumn(
                        ExportPathBuilder.Join([.. keyPrefix, childName, rowValue, columnValue]),
                        FormSchemaColumnKind.MatrixCell,
                        $"{template.GetSurveyJsTitle(childName)} — {rowText} — {columnText}",
                        MapMatrixCellDataType(columnElement),
                        SourceQuestion: childName,
                        MatrixRowValue: rowValue,
                        MatrixColumnValue: columnValue,
                        LoopPath: loopPath));
                }
            }

            return;
        }

        foreach ((var rowValue, var rowText) in SurveyJsChoiceHelper.EnumerateMatrixRows(template))
        {
            AddColumn(columns, seenKeys, limits, new FormSchemaColumn(
                ExportPathBuilder.Join([.. keyPrefix, childName, rowValue]),
                FormSchemaColumnKind.MatrixRow,
                $"{template.GetSurveyJsTitle(childName)} — {rowText}",
                "number",
                SourceQuestion: childName,
                MatrixRowValue: rowValue,
                MatrixColumnChoices: CollectMatrixColumnChoices(template),
                LoopPath: loopPath));
        }
    }

    private static void EmitLoopSourceMultipleTextColumns(
        JsonElement template,
        string childName,
        IReadOnlyList<string> keyPrefix,
        IReadOnlyList<LoopSegment> loopPath,
        List<FormSchemaColumn> columns,
        HashSet<string> seenKeys,
        SchemaCompilationLimits limits)
    {
        foreach ((var value, var text) in SurveyJsChoiceHelper.EnumerateMultipleTextItems(template))
        {
            AddColumn(columns, seenKeys, limits, new FormSchemaColumn(
                ExportPathBuilder.Join([.. keyPrefix, childName, value]),
                FormSchemaColumnKind.MultipleTextItem,
                $"{template.GetSurveyJsTitle(childName)} — {text}",
                "string",
                SourceQuestion: childName,
                MatrixRowValue: value,
                LoopPath: loopPath));
        }
    }

    private static void AddNestedLoopColumns(
        IReadOnlyList<PanelPath> panelPaths,
        HashSet<string> drivingCheckboxNames,
        List<FormSchemaColumn> columns,
        HashSet<string> seenKeys,
        SchemaCompilationLimits limits)
    {
        foreach (var panelPath in panelPaths)
        {
            foreach (var template in panelPath.TemplateElements)
            {
                var childType = template.GetSurveyJsType();
                var childName = template.GetSurveyJsName();

                if (string.IsNullOrWhiteSpace(childName) ||
                    SurveyJsElementType.IsNonData(childType) ||
                    SurveyJsElementType.PanelDynamic.Matches(childType) ||
                    drivingCheckboxNames.Contains(childName))
                {
                    continue;
                }

                foreach (var choiceValues in EnumerateChoiceCombinations(panelPath, limits))
                {
                    List<LoopSegment> loopSegments = new(panelPath.LoopPath.Count);
                    for (var i = 0; i < panelPath.LoopPath.Count; i++)
                    {
                        loopSegments.Add(new LoopSegment(
                            panelPath.LoopPath[i],
                            panelPath.PropertyPath[i],
                            choiceValues[i]));
                    }

                    var key = ExportPathBuilder.NestedLoopKey(choiceValues.AsSpan(), childName);
                    AddColumn(columns, seenKeys, limits, new FormSchemaColumn(
                        key,
                        FormSchemaColumnKind.NestedLoop,
                        $"{template.GetSurveyJsTitle(childName)} ({string.Join(" / ", choiceValues)})",
                        MapDataType(template, childType),
                        SourceQuestion: childName,
                        LoopPath: loopSegments));
                }
            }
        }
    }

    private static IEnumerable<string[]> EnumerateChoiceCombinations(
        PanelPath panelPath,
        SchemaCompilationLimits limits)
    {
        List<IReadOnlyList<string>> levels = [];
        List<int> levelSizes = [];

        for (var i = 0; i < panelPath.ChoicesPath.Count; i++)
        {
            var values = SurveyJsChoiceHelper.GetChoiceValues(panelPath.ChoicesPath[i]);

            if (values.Count > limits.MaxChoicesPerQuestion)
            {
                ThrowLimitExceeded(
                    SchemaCompilationLimitKind.MaxChoicesPerQuestion,
                    limits.MaxChoicesPerQuestion,
                    actual: values.Count,
                    context: $"loop level {i + 1}");
            }

            levels.Add(values);
            levelSizes.Add(values.Count);
        }

        var combinationCount = ChoiceCartesianProduct.EstimateCombinationCount(levelSizes);
        if (combinationCount == 0)
        {
            return [];
        }

        if (combinationCount > limits.MaxLoopCombinations)
        {
            ThrowLimitExceeded(
                SchemaCompilationLimitKind.MaxLoopCombinations,
                limits.MaxLoopCombinations,
                actual: combinationCount > int.MaxValue ? int.MaxValue : (int)combinationCount);
        }

        return ChoiceCartesianProduct.Enumerate(levels);
    }

    private static bool IsDrivingCheckbox(JsonElement element, string? type, string? name) =>
        SurveyJsElementType.IsDrivingChoiceType(type) &&
        !string.IsNullOrWhiteSpace(element.GetStringProperty(SurveyJsPropertyNames.ValuePropertyName)) &&
        !string.IsNullOrWhiteSpace(name);

    private static void AddColumn(
        List<FormSchemaColumn> columns,
        HashSet<string> seenKeys,
        SchemaCompilationLimits limits,
        FormSchemaColumn column)
    {
        if (!seenKeys.Add(column.Key))
        {
            throw new SchemaCompilationLimitExceededException(
                SchemaCompilationLimitKind.DuplicateColumnKey,
                limit: 1,
                message: $"Duplicate form schema column key '{column.Key}'.",
                context: column.Key);
        }

        if (columns.Count >= limits.MaxColumns)
        {
            ThrowLimitExceeded(
                SchemaCompilationLimitKind.MaxColumns,
                limits.MaxColumns,
                actual: columns.Count + 1);
        }

        columns.Add(column);
    }

    private static void ThrowLimitExceeded(
        SchemaCompilationLimitKind limitKind,
        int limit,
        int? actual = null,
        string? context = null)
    {
        var message = limitKind switch
        {
            SchemaCompilationLimitKind.MaxColumns =>
                $"Form schema column limit of {limit} exceeded.",
            SchemaCompilationLimitKind.MaxQuestions =>
                $"Form schema question limit of {limit} exceeded.",
            SchemaCompilationLimitKind.MaxChoicesPerQuestion when context is not null =>
                $"Choice limit of {limit} exceeded for question '{context}'.",
            SchemaCompilationLimitKind.MaxChoicesPerQuestion =>
                $"Choice limit of {limit} exceeded.",
            SchemaCompilationLimitKind.MaxNestingDepth =>
                $"Form schema nesting depth limit of {limit} exceeded.",
            SchemaCompilationLimitKind.MaxLoopCombinations =>
                $"Nested-loop combination limit of {limit} exceeded.",
            _ => $"Form schema compilation limit '{limitKind}' of {limit} exceeded.",
        };

        throw new SchemaCompilationLimitExceededException(limitKind, limit, message, actual, context);
    }

    private static string MapDataType(JsonElement element, string? type)
    {
        if (SurveyJsElementType.TryResolve(type)?.Category == SurveyJsElementCategory.File)
        {
            return "file";
        }

        if (SurveyJsElementType.Text.Matches(type) &&
            string.Equals(
                element.GetStringProperty(SurveyJsPropertyNames.InputType),
                "number",
                StringComparison.OrdinalIgnoreCase))
        {
            return "number";
        }

        return type?.ToLowerInvariant() switch
        {
            "boolean" => "boolean",
            "rating" or "slider" => "number",
            _ => "string",
        };
    }
}
