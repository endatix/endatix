using System.Text.Json;
using Endatix.Modules.Reporting.Domain.SurveyJs;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;
using Endatix.Modules.Reporting.Shared.SurveyJs;

namespace Endatix.Modules.Reporting.Features.FormSchema.Codebook;

/// <summary>
/// Builds format-neutral SurveyJS-aligned codebook metadata for BI export projection.
/// Shoji/Crunch-specific shapes are produced at export time, not stored here.
/// </summary>
internal static class FormSchemaCodebookBuilder
{
    private const int CurrentVersion = 1;

    internal static string Build(
        JsonElement definition,
        MergedFormSchema merged,
        string? existingCodebookJson = null)
    {
        var locales = SurveyJsLocalizationHelper.DiscoverLocales(definition);
        var questionElements = CollectQuestionElements(definition);
        var questions = BuildQuestions(questionElements);
        var existingColumns = TryParseExistingColumns(existingCodebookJson);
        var columns = BuildColumns(merged, questionElements, existingColumns);
        var choiceCatalogs = BuildChoiceCatalogs(questionElements);

        if (!string.IsNullOrWhiteSpace(existingCodebookJson))
        {
            MergeExisting(existingCodebookJson, questions, columns, choiceCatalogs);
        }

        return Serialize(locales, questions, columns, choiceCatalogs);
    }

    private static Dictionary<string, JsonElement> CollectQuestionElements(JsonElement definition)
    {
        Dictionary<string, JsonElement> elements = new(StringComparer.Ordinal);

        foreach (var page in EnumeratePages(definition))
        {
            if (!page.TryGetElements(out var pageElements))
            {
                continue;
            }

            CollectElements(pageElements, elements);
        }

        if (definition.TryGetElements(out var rootElements))
        {
            CollectElements(rootElements, elements);
        }

        return elements;
    }

    private static void CollectElements(JsonElement elementList, Dictionary<string, JsonElement> elements)
    {
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
            if (!string.IsNullOrWhiteSpace(name))
            {
                elements.TryAdd(name, element);
            }

            if (SurveyJsElementType.Panel.Matches(type) && element.TryGetElements(out var panelChildren))
            {
                CollectElements(panelChildren, elements);
            }

            if (SurveyJsElementType.PanelDynamic.Matches(type) &&
                element.TryGetTemplateElements(out var templateElements))
            {
                CollectElements(templateElements, elements);
            }
        }
    }

    private static Dictionary<string, JsonElement> BuildQuestions(
        IReadOnlyDictionary<string, JsonElement> questionElements)
    {
        Dictionary<string, JsonElement> questions = new(StringComparer.Ordinal);

        foreach (var entry in questionElements)
        {
            var name = entry.Key;
            var element = entry.Value;
            var type = element.GetSurveyJsType();

            if (string.IsNullOrWhiteSpace(type))
            {
                continue;
            }

            if (TryBuildQuestionEntry(element, type, out var questionEntry))
            {
                questions[name] = questionEntry;
            }
        }

        return questions;
    }

    private static Dictionary<string, JsonElement> BuildColumns(
        MergedFormSchema merged,
        IReadOnlyDictionary<string, JsonElement> questionElements,
        IReadOnlyDictionary<string, JsonElement>? existingColumns = null)
    {
        Dictionary<string, JsonElement> columns = new(StringComparer.Ordinal);

        foreach (var column in merged.Columns)
        {
            var parentKey = column.SourceQuestion ?? column.Key;
            var hasQuestion = questionElements.TryGetValue(parentKey, out var questionElement) &&
                               questionElement.ValueKind == JsonValueKind.Object;

            if (!hasQuestion &&
                existingColumns is not null &&
                existingColumns.TryGetValue(column.Key, out var existingColumn))
            {
                columns[column.Key] = existingColumn.Clone();
                continue;
            }

            columns[column.Key] = WriteColumnEntry(writer =>
            {
                writer.WriteString(FormSchemaCodebookPropertyNames.ParentKey, parentKey);
                writer.WriteString(
                    FormSchemaCodebookPropertyNames.SurveyJsType,
                    hasQuestion
                        ? questionElement.GetSurveyJsType() ?? FormSchemaCodebookPropertyNames.UnknownSurveyJsType
                        : FormSchemaCodebookPropertyNames.UnknownSurveyJsType);
                writer.WriteString(
                    FormSchemaCodebookPropertyNames.ExportShape,
                    hasQuestion
                        ? ResolveColumnExportShape(column, questionElement)
                        : ResolveColumnExportShapeWithoutQuestion(column));

                if (hasQuestion)
                {
                    WriteTitle(writer, questionElement);
                    WriteDescription(writer, questionElement);
                }

                WriteColumnChoiceMetadata(writer, column, hasQuestion, questionElement);
                WriteColumnMatrixMetadata(writer, column, hasQuestion, questionElement);
                WriteColumnLoopPathMetadata(writer, column);
            });
        }

        return columns;
    }

    private static void WriteColumnChoiceMetadata(
        Utf8JsonWriter writer,
        FormSchemaColumn column,
        bool hasQuestion,
        JsonElement questionElement)
    {
        if (string.IsNullOrWhiteSpace(column.ChoiceValue))
        {
            return;
        }

        writer.WriteString(FormSchemaPropertyNames.ChoiceValue, column.ChoiceValue);
        if (hasQuestion)
        {
            WriteChoiceLabel(writer, questionElement, column.ChoiceValue);
        }
    }

    private static void WriteColumnMatrixMetadata(
        Utf8JsonWriter writer,
        FormSchemaColumn column,
        bool hasQuestion,
        JsonElement questionElement)
    {
        if (!string.IsNullOrWhiteSpace(column.MatrixRowValue))
        {
            writer.WriteString(FormSchemaPropertyNames.MatrixRowValue, column.MatrixRowValue);
            if (hasQuestion)
            {
                WriteMatrixRowLabel(writer, questionElement, column.MatrixRowValue);
            }
        }

        if (!string.IsNullOrWhiteSpace(column.MatrixColumnValue))
        {
            writer.WriteString(FormSchemaPropertyNames.MatrixColumnValue, column.MatrixColumnValue);
            if (hasQuestion)
            {
                WriteMatrixColumnLabel(writer, questionElement, column.MatrixColumnValue);
            }
        }
    }

    private static void WriteColumnLoopPathMetadata(Utf8JsonWriter writer, FormSchemaColumn column)
    {
        if (column.LoopPath is not { Count: > 0 })
        {
            return;
        }

        WriteLoopPath(writer, column.LoopPath);
    }

    private static Dictionary<string, JsonElement> BuildChoiceCatalogs(
        IReadOnlyDictionary<string, JsonElement> questionElements)
    {
        Dictionary<string, JsonElement> catalogs = new(StringComparer.Ordinal);

        foreach (var entry in questionElements)
        {
            if (!HasChoiceCatalog(entry.Value))
            {
                continue;
            }

            catalogs[entry.Key] = WriteQuestionEntry(writer =>
            {
                writer.WritePropertyName(SurveyJsPropertyNames.Choices);
                writer.WriteStartArray();
                WriteCatalogChoices(writer, entry.Value);
                writer.WriteEndArray();
            });
        }

        return catalogs;
    }

    private static bool HasChoiceCatalog(JsonElement element)
    {
        var type = element.GetSurveyJsType();

        if (SurveyJsElementType.Boolean.Matches(type) ||
            SurveyJsElementType.ResolveFlattening(type, element) == SurveyJsFlattening.ChoiceIndicators)
        {
            return true;
        }

        if (SurveyJsElementType.Matrix.Matches(type) && !FormSchemaCodebookExportShape.HasMatrixCheckboxCells(element))
        {
            return true;
        }

        if (SurveyJsElementType.MatrixDropdown.Matches(type) || SurveyJsElementType.MatrixDynamic.Matches(type))
        {
            return HasMatrixQuestionLevelChoiceCatalog(element);
        }

        return SurveyJsElementType.Ranking.Matches(type);
    }

    private static bool HasMatrixQuestionLevelChoiceCatalog(JsonElement element) =>
        HasNonEmptyChoices(element) && HasChoiceBasedMatrixColumn(element);

    private static bool HasNonEmptyChoices(JsonElement element) =>
        element.TryGetChoices(out var choices) &&
        choices.ValueKind == JsonValueKind.Array &&
        choices.GetArrayLength() > 0;

    private static bool HasChoiceBasedMatrixColumn(JsonElement matrixElement)
    {
        foreach ((_, _, var columnElement) in SurveyJsChoiceHelper.EnumerateMatrixColumns(matrixElement))
        {
            if (IsChoiceBasedMatrixColumn(columnElement, matrixElement))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsChoiceBasedMatrixColumn(JsonElement columnElement, JsonElement matrixElement) =>
        IsChoiceBasedCellType(ResolveMatrixColumnCellType(columnElement, matrixElement));

    private static bool IsChoiceBasedCellType(string cellType) =>
        SurveyJsElementType.Dropdown.Matches(cellType) ||
        SurveyJsElementType.Radiogroup.Matches(cellType) ||
        SurveyJsElementType.Checkbox.Matches(cellType) ||
        SurveyJsElementType.Tagbox.Matches(cellType) ||
        SurveyJsElementType.Boolean.Matches(cellType) ||
        SurveyJsElementType.Ranking.Matches(cellType);

    private static string ResolveMatrixColumnCellType(JsonElement columnElement, JsonElement matrixElement)
    {
        var cellType = columnElement.GetStringProperty(SurveyJsPropertyNames.CellType);
        if (!string.IsNullOrWhiteSpace(cellType))
        {
            return cellType;
        }

        var defaultCellType = matrixElement.GetStringProperty(SurveyJsPropertyNames.DefaultCellType);
        if (!string.IsNullOrWhiteSpace(defaultCellType))
        {
            return defaultCellType;
        }

        if (SurveyJsElementType.MatrixDropdown.Matches(matrixElement.GetSurveyJsType()))
        {
            return SurveyJsElementType.Dropdown.Name;
        }

        return SurveyJsElementType.Text.Name;
    }

    private static JsonElement ResolveMatrixColumnChoicesSource(JsonElement matrixElement, JsonElement columnElement) =>
        HasNonEmptyChoices(columnElement) ? columnElement : matrixElement;

    private static bool TryBuildQuestionEntry(
        JsonElement element,
        string? type,
        out JsonElement questionEntry)
    {
        questionEntry = default;

        if (SurveyJsElementType.PanelDynamic.Matches(type))
        {
            questionEntry = WriteQuestionEntry(writer =>
            {
                writer.WriteString(FormSchemaCodebookPropertyNames.SurveyJsType, type!);
                WriteTitle(writer, element);
                WriteDescription(writer, element);

                if (element.TryGetLoopSource(out var _))
                {
                    WriteLoopSource(writer, element);
                    writer.WriteString(
                        FormSchemaCodebookPropertyNames.ExportShape,
                        FormSchemaCodebookExportShape.LoopPanel.Name);
                }
                else
                {
                    writer.WriteString(
                        FormSchemaCodebookPropertyNames.ExportShape,
                        FormSchemaCodebookExportShape.PanelDynamic.Name);
                }
            });
            return true;
        }

        if (SurveyJsElementType.Matrix.Matches(type) && !FormSchemaCodebookExportShape.HasMatrixCheckboxCells(element))
        {
            questionEntry = WriteQuestionEntry(writer =>
            {
                writer.WriteString(FormSchemaCodebookPropertyNames.SurveyJsType, type!);
                WriteTitle(writer, element);
                WriteDescription(writer, element);
                writer.WriteString(
                    FormSchemaCodebookPropertyNames.ExportShape,
                    FormSchemaCodebookExportShape.CategoricalArray.Name);
                WriteMatrixColumns(writer, element);
                WriteMatrixRows(writer, element);
            });
            return true;
        }

        if (SurveyJsElementType.ResolveFlattening(type, element) == SurveyJsFlattening.ChoiceIndicators)
        {
            questionEntry = WriteQuestionEntry(writer =>
            {
                writer.WriteString(FormSchemaCodebookPropertyNames.SurveyJsType, type!);
                WriteTitle(writer, element);
                WriteDescription(writer, element);
                writer.WriteString(
                    FormSchemaCodebookPropertyNames.ExportShape,
                    FormSchemaCodebookExportShape.MultipleResponse.Name);
                WriteChoices(writer, element);
            });
            return true;
        }

        if (SurveyJsElementType.Ranking.Matches(type))
        {
            questionEntry = WriteQuestionEntry(writer =>
            {
                writer.WriteString(FormSchemaCodebookPropertyNames.SurveyJsType, type!);
                WriteTitle(writer, element);
                WriteDescription(writer, element);
                writer.WriteString(
                    FormSchemaCodebookPropertyNames.ExportShape,
                    FormSchemaCodebookExportShape.Ranking.Name);
                WriteChoices(writer, element);
            });
            return true;
        }

        if (SurveyJsElementType.MultipleText.Matches(type))
        {
            questionEntry = WriteQuestionEntry(writer =>
            {
                writer.WriteString(FormSchemaCodebookPropertyNames.SurveyJsType, type!);
                WriteTitle(writer, element);
                WriteDescription(writer, element);
                writer.WriteString(
                    FormSchemaCodebookPropertyNames.ExportShape,
                    FormSchemaCodebookExportShape.MultipleText.Name);
                WriteMultipleTextItems(writer, element);
            });
            return true;
        }

        if (SurveyJsElementType.MatrixDropdown.Matches(type) || SurveyJsElementType.MatrixDynamic.Matches(type))
        {
            questionEntry = WriteQuestionEntry(writer =>
            {
                writer.WriteString(FormSchemaCodebookPropertyNames.SurveyJsType, type!);
                WriteTitle(writer, element);
                WriteDescription(writer, element);
                writer.WriteString(
                    FormSchemaCodebookPropertyNames.ExportShape,
                    FormSchemaCodebookExportShape.MatrixCell.Name);
                WriteMatrixDropdownColumns(writer, element);
                WriteMatrixRows(writer, element);
            });
            return true;
        }

        if (SurveyJsElementType.FileUpload.Matches(type))
        {
            questionEntry = WriteScalarQuestionEntry(element, type!, FormSchemaCodebookExportShape.File);
            return true;
        }

        questionEntry = WriteScalarQuestionEntry(element, type!, FormSchemaCodebookExportShape.Scalar);
        return true;
    }

    private static JsonElement WriteScalarQuestionEntry(
        JsonElement element,
        string type,
        FormSchemaCodebookExportShape exportShape)
    {
        return WriteQuestionEntry(writer =>
        {
            writer.WriteString(FormSchemaCodebookPropertyNames.SurveyJsType, type);
            WriteTitle(writer, element);
            WriteDescription(writer, element);
            writer.WriteString(FormSchemaCodebookPropertyNames.ExportShape, exportShape.Name);
            WriteInputType(writer, element);
        });
    }

    private static Dictionary<string, JsonElement>? TryParseExistingColumns(string? existingCodebookJson)
    {
        if (string.IsNullOrWhiteSpace(existingCodebookJson))
        {
            return null;
        }

        using var document = JsonDocument.Parse(existingCodebookJson);
        var root = document.RootElement;
        if (!root.TryGetProperty(FormSchemaCodebookPropertyNames.Columns, out var existingColumns) ||
            existingColumns.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        Dictionary<string, JsonElement> columns = new(StringComparer.Ordinal);
        foreach (var property in existingColumns.EnumerateObject())
        {
            columns[property.Name] = property.Value.Clone();
        }

        return columns;
    }

    private static void MergeExisting(
        string existingCodebookJson,
        Dictionary<string, JsonElement> questions,
        Dictionary<string, JsonElement> columns,
        Dictionary<string, JsonElement> choiceCatalogs)
    {
        using var document = JsonDocument.Parse(existingCodebookJson);
        var root = document.RootElement;

        if (root.TryGetProperty(FormSchemaCodebookPropertyNames.Questions, out var existingQuestions) &&
            existingQuestions.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in existingQuestions.EnumerateObject())
            {
                questions.TryAdd(property.Name, property.Value.Clone());
            }
        }

        if (root.TryGetProperty(FormSchemaCodebookPropertyNames.Columns, out var existingColumns) &&
            existingColumns.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in existingColumns.EnumerateObject())
            {
                columns.TryAdd(property.Name, property.Value.Clone());
            }
        }

        if (root.TryGetProperty(FormSchemaCodebookPropertyNames.ChoiceCatalogs, out var existingCatalogs) &&
            existingCatalogs.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in existingCatalogs.EnumerateObject())
            {
                if (!choiceCatalogs.TryGetValue(property.Name, out var currentCatalog))
                {
                    choiceCatalogs[property.Name] = property.Value.Clone();
                    continue;
                }

                choiceCatalogs[property.Name] = MergeChoiceCatalog(property.Value, currentCatalog);
            }
        }
    }

    private static JsonElement MergeChoiceCatalog(JsonElement existingCatalog, JsonElement currentCatalog)
    {
        var mergedByValue = LoadExistingChoiceCatalogEntries(existingCatalog);
        MergeCurrentChoiceCatalogEntries(mergedByValue, currentCatalog);
        return WriteMergedChoiceCatalog(mergedByValue);
    }

    private static Dictionary<string, (int Id, JsonElement Entry)> LoadExistingChoiceCatalogEntries(
        JsonElement existingCatalog)
    {
        Dictionary<string, (int Id, JsonElement Entry)> mergedByValue = new(StringComparer.Ordinal);

        if (!TryGetCatalogChoicesArray(existingCatalog, out var existingChoices))
        {
            return mergedByValue;
        }

        foreach (var choice in existingChoices.EnumerateArray())
        {
            var value = choice.GetStringProperty(SurveyJsPropertyNames.Value);
            if (value is null || !choice.TryGetProperty(FormSchemaCodebookPropertyNames.Id, out var idElement) ||
                idElement.ValueKind != JsonValueKind.Number)
            {
                continue;
            }

            mergedByValue[value] = (idElement.GetInt32(), choice.Clone());
        }

        return mergedByValue;
    }

    private static void MergeCurrentChoiceCatalogEntries(
        Dictionary<string, (int Id, JsonElement Entry)> mergedByValue,
        JsonElement currentCatalog)
    {
        if (!TryGetCatalogChoicesArray(currentCatalog, out var currentChoices))
        {
            return;
        }

        var nextId = mergedByValue.Count == 0 ? 1 : mergedByValue.Values.Max(entry => entry.Id) + 1;

        foreach (var choice in currentChoices.EnumerateArray())
        {
            var value = choice.GetStringProperty(SurveyJsPropertyNames.Value);
            if (value is null)
            {
                continue;
            }

            if (mergedByValue.TryGetValue(value, out var existing))
            {
                mergedByValue[value] = (existing.Id, ApplyChoiceId(choice, existing.Id));
                continue;
            }

            mergedByValue[value] = (nextId, ApplyChoiceId(choice, nextId));
            nextId++;
        }
    }

    private static JsonElement WriteMergedChoiceCatalog(
        Dictionary<string, (int Id, JsonElement Entry)> mergedByValue) =>
        WriteQuestionEntry(writer =>
        {
            writer.WritePropertyName(SurveyJsPropertyNames.Choices);
            writer.WriteStartArray();
            foreach ((_, var entry) in mergedByValue.Values.OrderBy(pair => pair.Id))
            {
                entry.WriteTo(writer);
            }

            writer.WriteEndArray();
        });

    private static bool TryGetCatalogChoicesArray(JsonElement catalog, out JsonElement choices) =>
        catalog.TryGetProperty(SurveyJsPropertyNames.Choices, out choices) &&
        choices.ValueKind == JsonValueKind.Array;

    private static JsonElement ApplyChoiceId(JsonElement choice, int id) =>
        WriteQuestionEntry(writer =>
        {
            var wroteId = false;
            foreach (var property in choice.EnumerateObject())
            {
                if (property.NameEquals(FormSchemaCodebookPropertyNames.Id))
                {
                    writer.WriteNumber(FormSchemaCodebookPropertyNames.Id, id);
                    wroteId = true;
                    continue;
                }

                property.WriteTo(writer);
            }

            if (!wroteId)
            {
                writer.WriteNumber(FormSchemaCodebookPropertyNames.Id, id);
            }
        });

    private static string Serialize(
        IReadOnlyList<string> locales,
        IReadOnlyDictionary<string, JsonElement> questions,
        IReadOnlyDictionary<string, JsonElement> columns,
        IReadOnlyDictionary<string, JsonElement> choiceCatalogs)
    {
        System.Buffers.ArrayBufferWriter<byte> buffer = new();
        using (Utf8JsonWriter writer = new(buffer))
        {
            writer.WriteStartObject();
            writer.WriteNumber(FormSchemaCodebookPropertyNames.Version, CurrentVersion);
            writer.WritePropertyName(FormSchemaCodebookPropertyNames.Locales);
            writer.WriteStartArray();
            foreach (var locale in locales)
            {
                writer.WriteStringValue(locale);
            }

            writer.WriteEndArray();

            WriteObjectDictionary(writer, FormSchemaCodebookPropertyNames.Questions, questions);
            WriteObjectDictionary(writer, FormSchemaCodebookPropertyNames.Columns, columns);
            WriteObjectDictionary(writer, FormSchemaCodebookPropertyNames.ChoiceCatalogs, choiceCatalogs);
            writer.WriteEndObject();
        }

        return System.Text.Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    private static void WriteObjectDictionary(
        Utf8JsonWriter writer,
        string propertyName,
        IReadOnlyDictionary<string, JsonElement> entries)
    {
        writer.WritePropertyName(propertyName);
        writer.WriteStartObject();
        foreach (var entry in entries.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            writer.WritePropertyName(entry.Key);
            entry.Value.WriteTo(writer);
        }

        writer.WriteEndObject();
    }

    private static string ResolveColumnExportShapeWithoutQuestion(FormSchemaColumn column) =>
        FormSchemaCodebookExportShape.FromColumnKind(column.Kind).Name;

    private static string ResolveColumnExportShape(FormSchemaColumn column, JsonElement questionElement) =>
        column.Kind switch
        {
            FormSchemaColumnKind.ChoiceIndicator or FormSchemaColumnKind.CheckboxOtherText =>
                FormSchemaCodebookExportShape.MultipleResponse.Name,
            FormSchemaColumnKind.MatrixRow => FormSchemaCodebookExportShape.CategoricalArray.Name,
            FormSchemaColumnKind.MatrixCell => FormSchemaCodebookExportShape.MatrixCell.Name,
            FormSchemaColumnKind.RankingChoice => FormSchemaCodebookExportShape.Ranking.Name,
            FormSchemaColumnKind.MultipleTextItem => FormSchemaCodebookExportShape.MultipleText.Name,
            FormSchemaColumnKind.FileUpload => FormSchemaCodebookExportShape.File.Name,
            _ => FormSchemaCodebookExportShape.FromQuestionElement(questionElement).Name,
        };

    private static IEnumerable<JsonElement> EnumeratePages(JsonElement definition)
    {
        if (!definition.TryGetPages(out var pages))
        {
            yield break;
        }

        foreach (var page in pages.EnumerateArray())
        {
            yield return page;
        }
    }

    private static void WriteTitle(Utf8JsonWriter writer, JsonElement element)
    {
        writer.WritePropertyName(SurveyJsPropertyNames.Title);
        SurveyJsLocalizationHelper.WriteLocalizedStrings(
            writer,
            SurveyJsLocalizationHelper.ReadLocalizedStrings(element, SurveyJsPropertyNames.Title));
    }

    private static void WriteDescription(Utf8JsonWriter writer, JsonElement element)
    {
        var description =
            SurveyJsLocalizationHelper.ReadLocalizedStrings(element, SurveyJsPropertyNames.Description);

        if (description.Count == 0)
        {
            return;
        }

        writer.WritePropertyName(SurveyJsPropertyNames.Description);
        SurveyJsLocalizationHelper.WriteLocalizedStrings(writer, description);
    }

    private static void WriteInputType(Utf8JsonWriter writer, JsonElement element)
    {
        var inputType = element.GetStringProperty(SurveyJsPropertyNames.InputType);
        if (!string.IsNullOrWhiteSpace(inputType))
        {
            writer.WriteString(SurveyJsPropertyNames.InputType, inputType);
        }
    }

    private static void WriteLoopSource(Utf8JsonWriter writer, JsonElement element)
    {
        writer.WritePropertyName(SurveyJsPropertyNames.LoopSource);
        writer.WriteStartArray();
        if (element.TryGetLoopSource(out var loopSource))
        {
            foreach (var source in loopSource.EnumerateArray())
            {
                if (source.ValueKind == JsonValueKind.String)
                {
                    writer.WriteStringValue(source.GetString());
                }
            }
        }

        writer.WriteEndArray();
    }

    private static void WriteLoopPath(Utf8JsonWriter writer, IReadOnlyList<LoopSegment> loopPath)
    {
        writer.WritePropertyName(FormSchemaPropertyNames.LoopPath);
        writer.WriteStartArray();
        foreach (var segment in loopPath)
        {
            writer.WriteStartObject();
            writer.WriteString(FormSchemaPropertyNames.PanelValueName, segment.PanelValueName);
            writer.WriteString(FormSchemaPropertyNames.PropertyName, segment.PropertyName);
            writer.WriteString(FormSchemaPropertyNames.ChoiceValue, segment.ChoiceValue);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    private static void WriteChoices(Utf8JsonWriter writer, JsonElement element)
    {
        writer.WritePropertyName(SurveyJsPropertyNames.Choices);
        writer.WriteStartArray();
        WriteCatalogChoices(writer, element);
        writer.WriteEndArray();
    }

    private static void WriteCatalogChoices(
        Utf8JsonWriter writer,
        JsonElement element,
        string? cellType = null)
    {
        if (SurveyJsElementType.Boolean.Matches(cellType ?? element.GetSurveyJsType()))
        {
            WriteChoice(
                writer,
                FormSchemaCodebookChoiceValues.True,
                1,
                SurveyJsLocalizationHelper.ReadLocalizedStrings(element, SurveyJsPropertyNames.LabelTrue),
                fallback: "Yes");
            WriteChoice(
                writer,
                FormSchemaCodebookChoiceValues.False,
                2,
                SurveyJsLocalizationHelper.ReadLocalizedStrings(element, SurveyJsPropertyNames.LabelFalse),
                fallback: "No");
            return;
        }

        var choiceId = 1;
        foreach ((var value, var text) in SurveyJsChoiceHelper.EnumerateChoices(element))
        {
            WriteChoice(
                writer,
                value,
                choiceId++,
                SurveyJsLocalizationHelper.ReadLocalizedStrings(
                    FindChoiceElement(element, value),
                    SurveyJsPropertyNames.Text),
                fallback: text);
        }

        if (element.GetBooleanProperty(SurveyJsPropertyNames.ShowOtherItem))
        {
            var otherText = SurveyJsLocalizationHelper.ReadLocalizedStrings(
                element,
                SurveyJsPropertyNames.OtherText);
            WriteChoice(writer, SurveyJsPropertyNames.Other, choiceId, otherText, fallback: "Other");
        }
    }

    private static void WriteMultipleTextItems(Utf8JsonWriter writer, JsonElement element)
    {
        writer.WritePropertyName(SurveyJsPropertyNames.Items);
        writer.WriteStartArray();
        foreach ((var value, var text) in SurveyJsChoiceHelper.EnumerateMultipleTextItems(element))
        {
            writer.WriteStartObject();
            writer.WriteString(SurveyJsPropertyNames.Name, value);
            writer.WritePropertyName(SurveyJsPropertyNames.Title);
            var localized = SurveyJsLocalizationHelper.ReadLocalizedStrings(
                FindMultipleTextItemElement(element, value),
                SurveyJsPropertyNames.Title);
            if (localized.Count == 0)
            {
                writer.WriteStartObject();
                writer.WriteString(FormSchemaCodebookPropertyNames.Default, text);
                writer.WriteEndObject();
            }
            else
            {
                SurveyJsLocalizationHelper.WriteLocalizedStrings(writer, localized);
            }

            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    private static void WriteMatrixColumns(Utf8JsonWriter writer, JsonElement element)
    {
        writer.WritePropertyName(SurveyJsPropertyNames.Columns);
        writer.WriteStartArray();
        var columnId = 1;
        foreach ((var value, var text, var columnElement) in SurveyJsChoiceHelper.EnumerateMatrixColumns(element))
        {
            WriteChoice(
                writer,
                value,
                columnId++,
                SurveyJsLocalizationHelper.ReadLocalizedStrings(columnElement, SurveyJsPropertyNames.Text),
                fallback: text);
        }

        writer.WriteEndArray();
    }

    private static void WriteMatrixDropdownColumns(Utf8JsonWriter writer, JsonElement element)
    {
        writer.WritePropertyName(SurveyJsPropertyNames.Columns);
        writer.WriteStartArray();
        foreach ((var value, var text, var columnElement) in SurveyJsChoiceHelper.EnumerateMatrixColumns(element))
        {
            writer.WriteStartObject();
            writer.WriteString(SurveyJsPropertyNames.Name, value);
            writer.WritePropertyName(SurveyJsPropertyNames.Title);
            var localized = SurveyJsLocalizationHelper.ReadLocalizedStrings(
                columnElement,
                SurveyJsPropertyNames.Title);
            if (localized.Count == 0)
            {
                writer.WriteStartObject();
                writer.WriteString(FormSchemaCodebookPropertyNames.Default, text);
                writer.WriteEndObject();
            }
            else
            {
                SurveyJsLocalizationHelper.WriteLocalizedStrings(writer, localized);
            }

            var cellType = columnElement.GetStringProperty(SurveyJsPropertyNames.CellType);
            if (!string.IsNullOrWhiteSpace(cellType))
            {
                writer.WriteString(SurveyJsPropertyNames.CellType, cellType);
            }

            var inputType = columnElement.GetStringProperty(SurveyJsPropertyNames.InputType);
            if (!string.IsNullOrWhiteSpace(inputType))
            {
                writer.WriteString(SurveyJsPropertyNames.InputType, inputType);
            }

            WriteMatrixColumnChoiceMetadata(writer, element, columnElement);

            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    private static void WriteMatrixColumnChoiceMetadata(
        Utf8JsonWriter writer,
        JsonElement matrixElement,
        JsonElement columnElement)
    {
        if (!IsChoiceBasedMatrixColumn(columnElement, matrixElement))
        {
            return;
        }

        var cellType = ResolveMatrixColumnCellType(columnElement, matrixElement);
        var choicesSource = ResolveMatrixColumnChoicesSource(matrixElement, columnElement);
        if (!SurveyJsElementType.Boolean.Matches(cellType) && !HasNonEmptyChoices(choicesSource))
        {
            return;
        }

        writer.WritePropertyName(SurveyJsPropertyNames.Choices);
        writer.WriteStartArray();
        WriteCatalogChoices(
            writer,
            choicesSource,
            SurveyJsElementType.Boolean.Matches(cellType) ? cellType : null);
        writer.WriteEndArray();
    }

    private static void WriteMatrixRows(Utf8JsonWriter writer, JsonElement element)
    {
        writer.WritePropertyName(SurveyJsPropertyNames.Rows);
        writer.WriteStartArray();
        foreach ((var value, var text) in SurveyJsChoiceHelper.EnumerateMatrixRows(element))
        {
            writer.WriteStartObject();
            writer.WriteString(SurveyJsPropertyNames.Value, value);
            writer.WritePropertyName(SurveyJsPropertyNames.Text);
            var localized = SurveyJsLocalizationHelper.ReadLocalizedStrings(
                FindMatrixRowElement(element, value),
                SurveyJsPropertyNames.Text);
            if (localized.Count == 0)
            {
                writer.WriteStartObject();
                writer.WriteString(FormSchemaCodebookPropertyNames.Default, text);
                writer.WriteEndObject();
            }
            else
            {
                SurveyJsLocalizationHelper.WriteLocalizedStrings(writer, localized);
            }

            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    private static void WriteChoiceLabel(
        Utf8JsonWriter writer,
        JsonElement questionElement,
        string choiceValue)
    {
        ResolveChoiceLabelTexts(questionElement, choiceValue, out var localized, out var fallback);
        writer.WritePropertyName(FormSchemaCodebookPropertyNames.ChoiceLabel);
        WriteLocalizedStringsWithFallback(writer, localized, fallback);
    }

    private static void ResolveChoiceLabelTexts(
        JsonElement questionElement,
        string choiceValue,
        out IReadOnlyDictionary<string, string> localized,
        out string fallback)
    {
        if (SurveyJsElementType.Boolean.Matches(questionElement.GetSurveyJsType()))
        {
            var propertyName = choiceValue == FormSchemaCodebookChoiceValues.True
                ? SurveyJsPropertyNames.LabelTrue
                : SurveyJsPropertyNames.LabelFalse;
            localized = SurveyJsLocalizationHelper.ReadLocalizedStrings(questionElement, propertyName);
            fallback = choiceValue == FormSchemaCodebookChoiceValues.True ? "Yes" : "No";
            return;
        }

        if (choiceValue == SurveyJsPropertyNames.Other)
        {
            localized = SurveyJsLocalizationHelper.ReadLocalizedStrings(questionElement, SurveyJsPropertyNames.OtherText);
            fallback = "Other";
            return;
        }

        localized = SurveyJsLocalizationHelper.ReadLocalizedStrings(
            FindChoiceElement(questionElement, choiceValue),
            SurveyJsPropertyNames.Text);
        fallback = ResolveChoiceFallbackText(questionElement, choiceValue);
    }

    private static string ResolveChoiceFallbackText(JsonElement questionElement, string choiceValue)
    {
        foreach ((var value, var text) in SurveyJsChoiceHelper.EnumerateChoices(questionElement))
        {
            if (string.Equals(value, choiceValue, StringComparison.Ordinal))
            {
                return text;
            }
        }

        return choiceValue;
    }

    private static void WriteLocalizedStringsWithFallback(
        Utf8JsonWriter writer,
        IReadOnlyDictionary<string, string> localizedText,
        string fallback)
    {
        if (localizedText.Count == 0)
        {
            writer.WriteStartObject();
            writer.WriteString(FormSchemaCodebookPropertyNames.Default, fallback);
            writer.WriteEndObject();
            return;
        }

        SurveyJsLocalizationHelper.WriteLocalizedStrings(writer, localizedText);
    }

    private static void WriteMatrixRowLabel(
        Utf8JsonWriter writer,
        JsonElement questionElement,
        string rowValue)
    {
        writer.WritePropertyName(FormSchemaCodebookPropertyNames.RowLabel);
        SurveyJsLocalizationHelper.WriteLocalizedStrings(
            writer,
            SurveyJsLocalizationHelper.ReadLocalizedStrings(
                FindMatrixRowElement(questionElement, rowValue),
                SurveyJsPropertyNames.Text));
    }

    private static void WriteMatrixColumnLabel(
        Utf8JsonWriter writer,
        JsonElement questionElement,
        string columnValue)
    {
        writer.WritePropertyName(FormSchemaCodebookPropertyNames.ColumnLabel);
        SurveyJsLocalizationHelper.WriteLocalizedStrings(
            writer,
            SurveyJsLocalizationHelper.ReadLocalizedStrings(
                FindMatrixColumnElement(questionElement, columnValue),
                SurveyJsPropertyNames.Title));
    }

    private static JsonElement FindChoiceElement(JsonElement element, string choiceValue)
    {
        if (!element.TryGetChoices(out var choices))
        {
            return default;
        }

        foreach (var choice in choices.EnumerateArray())
        {
            if (choice.ValueKind == JsonValueKind.String && choice.GetString() == choiceValue)
            {
                return choice;
            }

            if (choice.ValueKind == JsonValueKind.Object &&
                string.Equals(
                    choice.GetStringProperty(SurveyJsPropertyNames.Value) ?? choice.GetStringProperty(SurveyJsPropertyNames.Text),
                    choiceValue,
                    StringComparison.Ordinal))
            {
                return choice;
            }
        }

        return default;
    }

    private static JsonElement FindMultipleTextItemElement(JsonElement element, string itemName)
    {
        if (!element.TryGetItems(out var items))
        {
            return default;
        }

        foreach (var item in items.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.Object &&
                string.Equals(item.GetStringProperty(SurveyJsPropertyNames.Name), itemName, StringComparison.Ordinal))
            {
                return item;
            }
        }

        return default;
    }

    private static JsonElement FindMatrixRowElement(JsonElement element, string rowValue)
    {
        if (!element.TryGetRows(out var rows))
        {
            return default;
        }

        foreach (var row in rows.EnumerateArray())
        {
            if (row.ValueKind == JsonValueKind.String && row.GetString() == rowValue)
            {
                return row;
            }

            if (row.ValueKind == JsonValueKind.Object &&
                string.Equals(
                    row.GetStringProperty(SurveyJsPropertyNames.Value) ?? row.GetStringProperty(SurveyJsPropertyNames.Text),
                    rowValue,
                    StringComparison.Ordinal))
            {
                return row;
            }
        }

        return default;
    }

    private static JsonElement FindMatrixColumnElement(JsonElement element, string columnValue)
    {
        foreach ((var value, _, var columnElement) in SurveyJsChoiceHelper.EnumerateMatrixColumns(element))
        {
            if (string.Equals(value, columnValue, StringComparison.Ordinal))
            {
                return columnElement;
            }
        }

        return default;
    }

    private static void WriteChoice(
        Utf8JsonWriter writer,
        string value,
        int id,
        IReadOnlyDictionary<string, string> localizedText,
        string fallback)
    {
        writer.WriteStartObject();
        writer.WriteString(SurveyJsPropertyNames.Value, value);
        writer.WriteNumber(FormSchemaCodebookPropertyNames.Id, id);
        writer.WritePropertyName(SurveyJsPropertyNames.Text);
        WriteLocalizedStringsWithFallback(writer, localizedText, fallback);
        writer.WriteEndObject();
    }

    private static JsonElement WriteQuestionEntry(Action<Utf8JsonWriter> write)
    {
        System.Buffers.ArrayBufferWriter<byte> buffer = new();
        using (Utf8JsonWriter writer = new(buffer))
        {
            writer.WriteStartObject();
            write(writer);
            writer.WriteEndObject();
        }

        using var document = JsonDocument.Parse(System.Text.Encoding.UTF8.GetString(buffer.WrittenSpan));
        return document.RootElement.Clone();
    }

    private static JsonElement WriteColumnEntry(Action<Utf8JsonWriter> write) => WriteQuestionEntry(write);
}
