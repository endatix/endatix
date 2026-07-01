using System.Text.Json;

namespace Endatix.Modules.Reporting.Domain.SurveyJs;

/// <summary>
/// Walks a SurveyJS form definition and produces BI-ready codebook column definitions.
/// Mirrors semantics of <c>getPlainData</c> / nested-loop SQL export without a SurveyJS runtime.
/// </summary>
internal static class SurveyJsDefinitionFlattener
{
    private sealed record CollectedElement(JsonElement Element, int Depth, string? ParentValueName);

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

    public static IReadOnlyList<CodebookColumnDefinition> Flatten(
        JsonElement definition,
        FlatteningLimits? limits = null)
    {
        FlatteningLimits effectiveLimits = limits ?? FlatteningLimits.Default;
        List<CollectedElement> allElements = CollectElements(definition, effectiveLimits);
        HashSet<string> drivingCheckboxNames = allElements
            .Where(item => IsDrivingCheckbox(item.Element))
            .Select(item => GetElementName(item.Element)!)
            .ToHashSet(StringComparer.Ordinal);

        List<DynamicPanelNode> dynamicPanels = BuildDynamicPanelNodes(allElements, drivingCheckboxNames);
        List<PanelPath> panelPaths = BuildPanelPaths(dynamicPanels, effectiveLimits);

        List<CodebookColumnDefinition> columns = [];
        HashSet<string> seenKeys = new(StringComparer.Ordinal);

        AddSimpleColumns(allElements, drivingCheckboxNames, columns, seenKeys, effectiveLimits);
        AddCheckboxColumns(allElements, drivingCheckboxNames, columns, seenKeys, effectiveLimits);
        AddRankingColumns(allElements, columns, seenKeys, effectiveLimits);
        AddMatrixColumns(allElements, columns, seenKeys, effectiveLimits);
        AddStandalonePanelDynamicColumns(allElements, drivingCheckboxNames, columns, seenKeys, effectiveLimits);
        AddNestedLoopColumns(panelPaths, allElements, drivingCheckboxNames, columns, seenKeys, effectiveLimits);
        AddCalculatedValueColumns(definition, columns, seenKeys, effectiveLimits);

        if (columns.Count > effectiveLimits.MaxColumns)
        {
            throw new FlatteningLimitExceededException(
                $"Codebook column limit of {effectiveLimits.MaxColumns} exceeded.");
        }

        return columns;
    }

    private static List<CollectedElement> CollectElements(JsonElement definition, FlatteningLimits limits)
    {
        List<CollectedElement> elements = [];
        CollectFromContainer(definition, depth: 0, parentValueName: null, elements, limits);
        return elements;
    }

    private static void CollectFromContainer(
        JsonElement container,
        int depth,
        string? parentValueName,
        List<CollectedElement> elements,
        FlatteningLimits limits)
    {
        if (depth > limits.MaxNestingDepth)
        {
            return;
        }

        if (container.TryGetProperty("pages", out JsonElement pages) && pages.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement page in pages.EnumerateArray())
            {
                if (page.TryGetProperty("elements", out JsonElement pageElements))
                {
                    CollectElementList(pageElements, depth, parentValueName: null, elements, limits);
                }
            }
        }

        if (container.TryGetProperty("elements", out JsonElement rootElements))
        {
            CollectElementList(rootElements, depth, parentValueName: null, elements, limits);
        }
    }

    private static void CollectElementList(
        JsonElement elementList,
        int depth,
        string? parentValueName,
        List<CollectedElement> elements,
        FlatteningLimits limits)
    {
        if (elementList.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (JsonElement element in elementList.EnumerateArray())
        {
            string? type = GetElementType(element);
            if (SurveyJsElementTypes.IsNonData(type))
            {
                continue;
            }

            elements.Add(new CollectedElement(element, depth, parentValueName));

            if (string.Equals(type, "panel", StringComparison.OrdinalIgnoreCase) &&
                element.TryGetProperty("elements", out JsonElement panelChildren))
            {
                CollectElementList(panelChildren, depth, parentValueName, elements, limits);
            }

            if (string.Equals(type, "paneldynamic", StringComparison.OrdinalIgnoreCase) &&
                element.TryGetProperty("templateElements", out JsonElement templateElements))
            {
                string? panelValueName = element.TryGetProperty("valueName", out JsonElement valueNameProp)
                    ? valueNameProp.GetString()
                    : null;

                CollectElementList(templateElements, depth + 1, panelValueName, elements, limits);
            }
        }
    }

    private static List<DynamicPanelNode> BuildDynamicPanelNodes(
        IReadOnlyList<CollectedElement> allElements,
        HashSet<string> drivingCheckboxNames)
    {
        List<DynamicPanelNode> nodes = [];

        foreach (CollectedElement collected in allElements)
        {
            if (!string.Equals(GetElementType(collected.Element), "paneldynamic", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string? valueName = collected.Element.TryGetProperty("valueName", out JsonElement valueNameProp)
                ? valueNameProp.GetString()
                : null;

            if (string.IsNullOrWhiteSpace(valueName) || !drivingCheckboxNames.Contains(valueName))
            {
                continue;
            }

            CollectedElement? driver = allElements.FirstOrDefault(item =>
                string.Equals(GetElementName(item.Element), valueName, StringComparison.Ordinal));

            if (driver is null || !driver.Element.TryGetProperty("valuePropertyName", out JsonElement valuePropertyNameProp))
            {
                continue;
            }

            string? valuePropertyName = valuePropertyNameProp.GetString();
            if (string.IsNullOrWhiteSpace(valuePropertyName))
            {
                continue;
            }

            JsonElement choices = driver.Element.TryGetProperty("choices", out JsonElement choicesProp)
                ? choicesProp
                : default;

            JsonElement[] templateElements = collected.Element.TryGetProperty("templateElements", out JsonElement templateProp) &&
                                             templateProp.ValueKind == JsonValueKind.Array
                ? templateProp.EnumerateArray().ToArray()
                : [];

            nodes.Add(new DynamicPanelNode(
                GetElementName(collected.Element) ?? valueName,
                valueName,
                valuePropertyName,
                collected.ParentValueName,
                choices,
                templateElements));
        }

        return nodes;
    }

    private static List<PanelPath> BuildPanelPaths(
        IReadOnlyList<DynamicPanelNode> dynamicPanels,
        FlatteningLimits limits)
    {
        Dictionary<string, DynamicPanelNode> panelsByValueName = dynamicPanels.ToDictionary(
            panel => panel.ValueName,
            StringComparer.Ordinal);

        List<PanelPath> paths = [];
        IEnumerable<DynamicPanelNode> roots = dynamicPanels.Where(panel => panel.ParentValueName is null);

        foreach (DynamicPanelNode root in roots)
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
        FlatteningLimits limits)
    {
        if (loopPath.Count >= limits.MaxNestingDepth)
        {
            return;
        }

        List<string> nextLoopPath = [.. loopPath, panel.ValueName];
        List<string> nextPropertyPath = [.. propertyPath, panel.ValuePropertyName];
        List<JsonElement> nextChoicesPath = [.. choicesPath, panel.Choices];

        DynamicPanelNode? childPanel = panel.TemplateElements
            .Where(template => string.Equals(GetElementType(template), "paneldynamic", StringComparison.OrdinalIgnoreCase))
            .Select(template =>
            {
                string? childValueName = template.TryGetProperty("valueName", out JsonElement valueNameProp)
                    ? valueNameProp.GetString()
                    : null;

                if (childValueName is not null && panelsByValueName.TryGetValue(childValueName, out DynamicPanelNode child))
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
        List<CodebookColumnDefinition> columns,
        HashSet<string> seenKeys,
        FlatteningLimits limits)
    {
        if (!definition.TryGetProperty("calculatedValues", out JsonElement calculatedValues) ||
            calculatedValues.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (JsonElement calculatedValue in calculatedValues.EnumerateArray())
        {
            if (calculatedValue.TryGetProperty("includeIntoResult", out JsonElement includeProp) &&
                includeProp.ValueKind == JsonValueKind.False)
            {
                continue;
            }

            string? name = calculatedValue.TryGetProperty("name", out JsonElement nameProp)
                ? nameProp.GetString()
                : null;

            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            AddColumn(columns, seenKeys, limits, new CodebookColumnDefinition(
                name,
                CodebookColumnKind.Calculated,
                name,
                "string"));
        }
    }

    private static void AddSimpleColumns(
        IReadOnlyList<CollectedElement> allElements,
        HashSet<string> drivingCheckboxNames,
        List<CodebookColumnDefinition> columns,
        HashSet<string> seenKeys,
        FlatteningLimits limits)
    {
        foreach (CollectedElement collected in allElements.Where(item => item.Depth == 0))
        {
            string? type = GetElementType(collected.Element);
            string? name = GetElementName(collected.Element);

            if (string.IsNullOrWhiteSpace(name) ||
                SurveyJsElementTypes.IsNonData(type) ||
                SurveyJsElementTypes.ContainerTypes.Contains(type!) ||
                string.Equals(type, "checkbox", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(type, "tagbox", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(type, "ranking", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(type, "matrix", StringComparison.OrdinalIgnoreCase) ||
                drivingCheckboxNames.Contains(name))
            {
                continue;
            }

            AddColumn(columns, seenKeys, limits, new CodebookColumnDefinition(
                name,
                CodebookColumnKind.Simple,
                GetElementTitle(collected.Element, name),
                MapDataType(type)));
        }
    }

    private static void AddCheckboxColumns(
        IReadOnlyList<CollectedElement> allElements,
        HashSet<string> drivingCheckboxNames,
        List<CodebookColumnDefinition> columns,
        HashSet<string> seenKeys,
        FlatteningLimits limits)
    {
        foreach (CollectedElement collected in allElements.Where(item => item.Depth == 0))
        {
            string? type = GetElementType(collected.Element);
            string? name = GetElementName(collected.Element);

            if (string.IsNullOrWhiteSpace(name) ||
                drivingCheckboxNames.Contains(name) ||
                (!string.Equals(type, "checkbox", StringComparison.OrdinalIgnoreCase) &&
                 !string.Equals(type, "tagbox", StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            int choiceCount = 0;
            foreach ((string value, string text) in SurveyJsChoiceHelper.EnumerateChoices(collected.Element))
            {
                if (++choiceCount > limits.MaxChoicesPerQuestion)
                {
                    throw new FlatteningLimitExceededException(
                        $"Choice limit of {limits.MaxChoicesPerQuestion} exceeded for question '{name}'.");
                }

                string key = ExportPathBuilder.CheckboxChoiceKey(name, value);
                AddColumn(columns, seenKeys, limits, new CodebookColumnDefinition(
                    key,
                    CodebookColumnKind.CheckboxChoice,
                    $"{GetElementTitle(collected.Element, name)} — {text}",
                    "boolean",
                    SourceQuestion: name,
                    ChoiceValue: value));
            }

            if (collected.Element.TryGetProperty("showOtherItem", out JsonElement showOtherProp) &&
                showOtherProp.ValueKind == JsonValueKind.True)
            {
                AddColumn(columns, seenKeys, limits, new CodebookColumnDefinition(
                    ExportPathBuilder.CheckboxOtherTextKey(name),
                    CodebookColumnKind.CheckboxOtherText,
                    $"{GetElementTitle(collected.Element, name)} — Other",
                    "string",
                    SourceQuestion: name));
            }
        }
    }

    private static void AddRankingColumns(
        IReadOnlyList<CollectedElement> allElements,
        List<CodebookColumnDefinition> columns,
        HashSet<string> seenKeys,
        FlatteningLimits limits)
    {
        foreach (CollectedElement collected in allElements.Where(item => item.Depth == 0))
        {
            string? type = GetElementType(collected.Element);
            string? name = GetElementName(collected.Element);

            if (string.IsNullOrWhiteSpace(name) ||
                !string.Equals(type, "ranking", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            int choiceCount = 0;
            foreach ((string value, string text) in SurveyJsChoiceHelper.EnumerateChoices(collected.Element))
            {
                if (++choiceCount > limits.MaxChoicesPerQuestion)
                {
                    throw new FlatteningLimitExceededException(
                        $"Choice limit of {limits.MaxChoicesPerQuestion} exceeded for question '{name}'.");
                }

                string key = ExportPathBuilder.RankingChoiceKey(name, value);
                AddColumn(columns, seenKeys, limits, new CodebookColumnDefinition(
                    key,
                    CodebookColumnKind.RankingChoice,
                    $"{GetElementTitle(collected.Element, name)} — {text}",
                    "number",
                    SourceQuestion: name,
                    ChoiceValue: value));
            }
        }
    }

    private static void AddMatrixColumns(
        IReadOnlyList<CollectedElement> allElements,
        List<CodebookColumnDefinition> columns,
        HashSet<string> seenKeys,
        FlatteningLimits limits)
    {
        foreach (CollectedElement collected in allElements.Where(item => item.Depth == 0))
        {
            string? type = GetElementType(collected.Element);
            string? name = GetElementName(collected.Element);

            if (string.IsNullOrWhiteSpace(name) ||
                !string.Equals(type, "matrix", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            foreach ((string value, string text) in SurveyJsChoiceHelper.EnumerateMatrixRows(collected.Element))
            {
                string key = ExportPathBuilder.MatrixRowKey(name, value);
                AddColumn(columns, seenKeys, limits, new CodebookColumnDefinition(
                    key,
                    CodebookColumnKind.MatrixRow,
                    $"{GetElementTitle(collected.Element, name)} — {text}",
                    "string",
                    SourceQuestion: name,
                    MatrixRowValue: value));
            }
        }
    }

    private static void AddStandalonePanelDynamicColumns(
        IReadOnlyList<CollectedElement> allElements,
        HashSet<string> drivingCheckboxNames,
        List<CodebookColumnDefinition> columns,
        HashSet<string> seenKeys,
        FlatteningLimits limits)
    {
        foreach (CollectedElement collected in allElements.Where(item => item.Depth == 0))
        {
            string? type = GetElementType(collected.Element);
            string? name = GetElementName(collected.Element);

            if (string.IsNullOrWhiteSpace(name) ||
                !string.Equals(type, "paneldynamic", StringComparison.OrdinalIgnoreCase) ||
                (collected.Element.TryGetProperty("valueName", out JsonElement valueNameProp) &&
                 !string.IsNullOrWhiteSpace(valueNameProp.GetString()) &&
                 drivingCheckboxNames.Contains(valueNameProp.GetString()!)))
            {
                continue;
            }

            if (!collected.Element.TryGetProperty("templateElements", out JsonElement templateElements) ||
                templateElements.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            int maxPanelCount = collected.Element.TryGetProperty("maxPanelCount", out JsonElement maxPanelCountProp) &&
                                maxPanelCountProp.ValueKind == JsonValueKind.Number
                ? maxPanelCountProp.GetInt32()
                : limits.MaxPanelCount;

            for (int index = 0; index < maxPanelCount; index++)
            {
                foreach (JsonElement template in templateElements.EnumerateArray())
                {
                    string? childType = GetElementType(template);
                    string? childName = GetElementName(template);

                    if (string.IsNullOrWhiteSpace(childName) ||
                        SurveyJsElementTypes.IsNonData(childType) ||
                        string.Equals(childType, "paneldynamic", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    string key = ExportPathBuilder.PanelIndexKey(name, index, childName);
                    AddColumn(columns, seenKeys, limits, new CodebookColumnDefinition(
                        key,
                        CodebookColumnKind.PanelDynamicIndex,
                        $"{GetElementTitle(template, childName)} ({name} #{index + 1})",
                        MapDataType(childType),
                        SourceQuestion: childName,
                        PanelName: name,
                        PanelIndex: index));
                }
            }
        }
    }

    private static void AddNestedLoopColumns(
        IReadOnlyList<PanelPath> panelPaths,
        IReadOnlyList<CollectedElement> allElements,
        HashSet<string> drivingCheckboxNames,
        List<CodebookColumnDefinition> columns,
        HashSet<string> seenKeys,
        FlatteningLimits limits)
    {
        foreach (PanelPath panelPath in panelPaths)
        {
            List<IReadOnlyList<string>> choiceCombinations = BuildChoiceCombinations(panelPath, limits);

            foreach (JsonElement template in panelPath.TemplateElements)
            {
                string? childType = GetElementType(template);
                string? childName = GetElementName(template);

                if (string.IsNullOrWhiteSpace(childName) ||
                    SurveyJsElementTypes.IsNonData(childType) ||
                    string.Equals(childType, "paneldynamic", StringComparison.OrdinalIgnoreCase) ||
                    drivingCheckboxNames.Contains(childName))
                {
                    continue;
                }

                foreach (IReadOnlyList<string> choiceValues in choiceCombinations)
                {
                    List<LoopSegment> loopSegments = [];
                    for (int i = 0; i < panelPath.LoopPath.Count; i++)
                    {
                        loopSegments.Add(new LoopSegment(
                            panelPath.LoopPath[i],
                            panelPath.PropertyPath[i],
                            choiceValues[i]));
                    }

                    string key = ExportPathBuilder.NestedLoopKey([.. choiceValues], childName);
                    AddColumn(columns, seenKeys, limits, new CodebookColumnDefinition(
                        key,
                        CodebookColumnKind.NestedLoop,
                        $"{GetElementTitle(template, childName)} ({string.Join(" / ", choiceValues)})",
                        MapDataType(childType),
                        SourceQuestion: childName,
                        LoopPath: loopSegments));
                }
            }
        }
    }

    private static List<IReadOnlyList<string>> BuildChoiceCombinations(
        PanelPath panelPath,
        FlatteningLimits limits)
    {
        List<IReadOnlyList<string>> levels = [];

        for (int i = 0; i < panelPath.ChoicesPath.Count; i++)
        {
            JsonElement choicesElement = panelPath.ChoicesPath[i];
            List<string> values = [];

            if (choicesElement.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement choice in choicesElement.EnumerateArray())
                {
                    if (choice.ValueKind == JsonValueKind.String)
                    {
                        values.Add(choice.GetString()!);
                    }
                    else if (choice.ValueKind == JsonValueKind.Object)
                    {
                        string? value = choice.TryGetProperty("value", out JsonElement valueProp)
                            ? valueProp.GetString()
                            : choice.TryGetProperty("text", out JsonElement textProp)
                                ? textProp.GetString()
                                : null;

                        if (value is not null)
                        {
                            values.Add(value);
                        }
                    }
                }
            }

            if (values.Count > limits.MaxChoicesPerQuestion)
            {
                throw new FlatteningLimitExceededException(
                    $"Choice limit of {limits.MaxChoicesPerQuestion} exceeded for loop level {i + 1}.");
            }

            levels.Add(values);
        }

        return CartesianProduct(levels).ToList();
    }

    private static IEnumerable<IReadOnlyList<string>> CartesianProduct(IReadOnlyList<IReadOnlyList<string>> levels)
    {
        IEnumerable<IReadOnlyList<string>> results = [Array.Empty<string>()];

        foreach (IReadOnlyList<string> level in levels)
        {
            results = results.SelectMany(prefix =>
                level.Select(value => (IReadOnlyList<string>)[.. prefix, value]));
        }

        foreach (IReadOnlyList<string> result in results)
        {
            yield return result;
        }
    }

    private static bool IsDrivingCheckbox(JsonElement element) =>
        SurveyJsElementTypes.IsDrivingChoiceType(GetElementType(element)) &&
        element.TryGetProperty("valuePropertyName", out JsonElement valuePropertyNameProp) &&
        !string.IsNullOrWhiteSpace(valuePropertyNameProp.GetString()) &&
        !string.IsNullOrWhiteSpace(GetElementName(element));

    private static void AddColumn(
        List<CodebookColumnDefinition> columns,
        HashSet<string> seenKeys,
        FlatteningLimits limits,
        CodebookColumnDefinition column)
    {
        if (!seenKeys.Add(column.Key))
        {
            return;
        }

        if (columns.Count >= limits.MaxColumns)
        {
            throw new FlatteningLimitExceededException(
                $"Codebook column limit of {limits.MaxColumns} exceeded.");
        }

        columns.Add(column);
    }

    private static string? GetElementType(JsonElement element) =>
        element.TryGetProperty("type", out JsonElement typeProp) ? typeProp.GetString() : null;

    private static string? GetElementName(JsonElement element) =>
        element.TryGetProperty("name", out JsonElement nameProp) ? nameProp.GetString() : null;

    private static string GetElementTitle(JsonElement element, string fallback) =>
        element.TryGetProperty("title", out JsonElement titleProp) && !string.IsNullOrWhiteSpace(titleProp.GetString())
            ? titleProp.GetString()!
            : fallback;

    private static string MapDataType(string? type) => type?.ToLowerInvariant() switch
    {
        "boolean" => "boolean",
        "number" or "rating" => "number",
        "file" => "file",
        _ => "string",
    };
}
