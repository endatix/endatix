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

    public static IReadOnlyList<FormSchemaColumn> Flatten(
        JsonElement definition,
        SchemaCompilationLimits? limits = null)
    {
        var effectiveLimits = limits ?? SchemaCompilationLimits.Default;
        var allElements = CollectElements(definition, effectiveLimits);
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

    private static void CollectFromContainer(
        JsonElement container,
        List<CollectedElement> elements,
        SchemaCompilationLimits limits)
    {
        if (container.TryGetProperty("pages", out var pages) && pages.ValueKind == JsonValueKind.Array)
        {
            foreach (var page in pages.EnumerateArray())
            {
                if (page.TryGetProperty("elements", out var pageElements))
                {
                    CollectElementList(pageElements, depth: 0, parentValueName: null, elements, limits);
                }
            }
        }

        if (container.TryGetProperty("elements", out var rootElements))
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
            var type = GetElementType(element);
            if (SurveyJsElementType.IsNonData(type))
            {
                continue;
            }

            var name = GetElementName(element);
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
            !element.TryGetProperty("elements", out var panelChildren))
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
            !element.TryGetProperty("elements", out var pageChildren))
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
            !element.TryGetProperty("templateElements", out var templateElements))
        {
            return;
        }

        var panelValueName = element.TryGetProperty("valueName", out var valueNameProp)
            ? valueNameProp.GetString()
            : null;

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

            var valueName = collected.Element.TryGetProperty("valueName", out var valueNameProp)
                ? valueNameProp.GetString()
                : null;

            if (!TryResolveDynamicPanelDriver(valueName, elementsByName, drivingCheckboxNames, out var driver, out var valuePropertyName, out var choices))
            {
                continue;
            }

            var templateElements = collected.Element.TryGetProperty("templateElements", out var templateProp) &&
                                             templateProp.ValueKind == JsonValueKind.Array
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

        if (!elementsByName.TryGetValue(valueName!, out var resolvedDriver) ||
            !resolvedDriver.Element.TryGetProperty("valuePropertyName", out var valuePropertyNameProp))
        {
            return false;
        }

        driver = resolvedDriver;

        valuePropertyName = valuePropertyNameProp.GetString() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(valuePropertyName))
        {
            return false;
        }

        choices = driver.Element.TryGetProperty("choices", out var choicesProp)
            ? choicesProp
            : default;

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
            .Where(template => SurveyJsElementType.PanelDynamic.Matches(GetElementType(template)))
            .Select(template =>
            {
                var childValueName = template.TryGetProperty("valueName", out var valueNameProp)
                    ? valueNameProp.GetString()
                    : null;

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
        if (!definition.TryGetProperty("calculatedValues", out var calculatedValues) ||
            calculatedValues.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var calculatedValue in calculatedValues.EnumerateArray())
        {
            if (calculatedValue.TryGetProperty("includeIntoResult", out var includeProp) &&
                includeProp.ValueKind == JsonValueKind.False)
            {
                continue;
            }

            var name = calculatedValue.TryGetProperty("name", out var nameProp)
                ? nameProp.GetString()
                : null;

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
                case SurveyJsFlattening.CheckboxChoices:
                    EmitCheckboxColumns(collected, drivingCheckboxNames, columns, seenKeys, limits);
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
                    EmitStandalonePanelDynamicColumns(collected, drivingCheckboxNames, columns, seenKeys, limits);
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
            GetElementTitle(collected.Element, name),
            MapDataType(collected.Element, type)));
    }

    private static void EmitCheckboxColumns(
        CollectedElement collected,
        HashSet<string> drivingCheckboxNames,
        List<FormSchemaColumn> columns,
        HashSet<string> seenKeys,
        SchemaCompilationLimits limits)
    {
        var name = collected.Name!;

        if (drivingCheckboxNames.Contains(name))
        {
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

            var key = ExportPathBuilder.CheckboxChoiceKey(name, value);
            AddColumn(columns, seenKeys, limits, new FormSchemaColumn(
                key,
                FormSchemaColumnKind.CheckboxChoice,
                $"{GetElementTitle(collected.Element, name)} — {text}",
                "boolean",
                SourceQuestion: name,
                ChoiceValue: value));
        }

        if (collected.Element.TryGetProperty("showOtherItem", out var showOtherProp) &&
            showOtherProp.ValueKind == JsonValueKind.True)
        {
            AddColumn(columns, seenKeys, limits, new FormSchemaColumn(
                ExportPathBuilder.CheckboxOtherTextKey(name),
                FormSchemaColumnKind.CheckboxOtherText,
                $"{GetElementTitle(collected.Element, name)} — Other",
                "string",
                SourceQuestion: name));
        }
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
                $"{GetElementTitle(collected.Element, name)} — {text}",
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
                $"{GetElementTitle(collected.Element, name)} — {text}",
                "string",
                SourceQuestion: name,
                MatrixRowValue: value));
        }
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
                    $"{GetElementTitle(collected.Element, name)} — {rowText} — {columnText}",
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
                    $"{GetElementTitle(collected.Element, name)} — #{rowIndex + 1} — {columnText}",
                    MapMatrixCellDataType(columnElement),
                    SourceQuestion: name,
                    PanelIndex: rowIndex,
                    MatrixColumnValue: columnValue));
            }
        }
    }

    private static int ResolveMatrixRowCount(JsonElement matrixElement, SchemaCompilationLimits limits)
    {
        var configuredCount = matrixElement.TryGetProperty("maxRowCount", out var maxRowCountProp) &&
                              maxRowCountProp.ValueKind == JsonValueKind.Number
            ? maxRowCountProp.GetInt32()
            : matrixElement.TryGetProperty("rowCount", out var rowCountProp) &&
              rowCountProp.ValueKind == JsonValueKind.Number
                ? rowCountProp.GetInt32()
                : limits.MaxMatrixRowCount;

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

        if (columnElement.TryGetProperty("inputType", out var inputType) &&
            string.Equals(inputType.GetString(), "number", StringComparison.OrdinalIgnoreCase))
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
                $"{GetElementTitle(collected.Element, name)} — {text}",
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
            GetElementTitle(collected.Element, name),
            "file"));
    }

    private static void EmitStandalonePanelDynamicColumns(
        CollectedElement collected,
        HashSet<string> drivingCheckboxNames,
        List<FormSchemaColumn> columns,
        HashSet<string> seenKeys,
        SchemaCompilationLimits limits)
    {
        var name = collected.Name!;

        if (collected.Element.TryGetProperty("valueName", out var valueNameProp) &&
            !string.IsNullOrWhiteSpace(valueNameProp.GetString()) &&
            drivingCheckboxNames.Contains(valueNameProp.GetString()!))
        {
            return;
        }

        if (!collected.Element.TryGetProperty("templateElements", out var templateElements) ||
            templateElements.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        var maxPanelCount = ResolvePanelCount(collected.Element, limits);

        for (var index = 0; index < maxPanelCount; index++)
        {
            foreach (var template in templateElements.EnumerateArray())
            {
                var childType = GetElementType(template);
                var childName = GetElementName(template);

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
                    $"{GetElementTitle(template, childName)} ({name} #{index + 1})",
                    MapDataType(template, childType),
                    SourceQuestion: childName,
                    PanelName: name,
                    PanelIndex: index));
            }
        }
    }

    private static int ResolvePanelCount(JsonElement panelElement, SchemaCompilationLimits limits)
    {
        var configuredCount = panelElement.TryGetProperty("maxPanelCount", out var maxPanelCountProp) &&
                              maxPanelCountProp.ValueKind == JsonValueKind.Number
            ? maxPanelCountProp.GetInt32()
            : limits.MaxPanelCount;

        if (configuredCount < 0)
        {
            configuredCount = 0;
        }

        return Math.Min(configuredCount, limits.MaxPanelCount);
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
                var childType = GetElementType(template);
                var childName = GetElementName(template);

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
                        $"{GetElementTitle(template, childName)} ({string.Join(" / ", choiceValues)})",
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
        element.TryGetProperty("valuePropertyName", out var valuePropertyNameProp) &&
        !string.IsNullOrWhiteSpace(valuePropertyNameProp.GetString()) &&
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

    private static string? GetElementType(JsonElement element) =>
        element.TryGetProperty("type", out var typeProp) ? typeProp.GetString() : null;

    private static string? GetElementName(JsonElement element) =>
        element.TryGetProperty("name", out var nameProp) ? nameProp.GetString() : null;

    private static string GetElementTitle(JsonElement element, string fallback) =>
        element.TryGetProperty("title", out var titleProp) &&
        titleProp.ValueKind == JsonValueKind.String &&
        !string.IsNullOrWhiteSpace(titleProp.GetString())
            ? titleProp.GetString()!
            : fallback;

    private static string MapDataType(JsonElement element, string? type)
    {
        if (SurveyJsElementType.TryResolve(type)?.Category == SurveyJsElementCategory.File)
        {
            return "file";
        }

        if (SurveyJsElementType.Text.Matches(type) &&
            element.TryGetProperty("inputType", out var inputType) &&
            string.Equals(inputType.GetString(), "number", StringComparison.OrdinalIgnoreCase))
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
