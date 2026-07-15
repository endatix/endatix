using Ardalis.GuardClauses;
using Endatix.Modules.Reporting.Domain.SurveyJs;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;
using Endatix.Modules.Reporting.Shared.SurveyJs;
using System.Text.Json;

namespace Endatix.Modules.Reporting.Features.FormSchema.Codebook;

/// <summary>
/// Canonical export-shape values stored in form-schema codebook metadata.
/// </summary>
internal sealed record FormSchemaCodebookExportShape
{
    private FormSchemaCodebookExportShape(string name)
    {
        Guard.Against.NullOrWhiteSpace(name);

        Name = name;
    }

    public string Name { get; }

    public static readonly FormSchemaCodebookExportShape LoopPanel = new("loop_panel");
    public static readonly FormSchemaCodebookExportShape PanelDynamic = new("panel_dynamic");
    public static readonly FormSchemaCodebookExportShape CategoricalArray = new("categorical_array");
    public static readonly FormSchemaCodebookExportShape MultipleResponse = new("multiple_response");
    public static readonly FormSchemaCodebookExportShape Ranking = new("ranking");
    public static readonly FormSchemaCodebookExportShape MultipleText = new("multiple_text");
    public static readonly FormSchemaCodebookExportShape MatrixCell = new("matrix_cell");
    public static readonly FormSchemaCodebookExportShape File = new("file");
    public static readonly FormSchemaCodebookExportShape Scalar = new("scalar");

    public static FormSchemaCodebookExportShape FromColumnKind(FormSchemaColumnKind kind) =>
        kind switch
        {
            FormSchemaColumnKind.ChoiceIndicator or FormSchemaColumnKind.CheckboxOtherText => MultipleResponse,
            FormSchemaColumnKind.MatrixRow => CategoricalArray,
            FormSchemaColumnKind.MatrixCell => MatrixCell,
            FormSchemaColumnKind.RankingChoice => Ranking,
            FormSchemaColumnKind.MultipleTextItem => MultipleText,
            FormSchemaColumnKind.FileUpload => File,
            _ => Scalar,
        };

    public static FormSchemaCodebookExportShape FromQuestionElement(JsonElement questionElement)
    {
        var type = questionElement.GetSurveyJsType();

        if (SurveyJsElementType.PanelDynamic.Matches(type) && questionElement.TryGetLoopSource(out var _))
        {
            return LoopPanel;
        }

        if (SurveyJsElementType.Boolean.Matches(type))
        {
            return Scalar;
        }

        if (SurveyJsElementType.Matrix.Matches(type) && !HasMatrixCheckboxCells(questionElement))
        {
            return CategoricalArray;
        }

        if (SurveyJsElementType.ResolveFlattening(type) == SurveyJsFlattening.ChoiceIndicators)
        {
            return MultipleResponse;
        }

        if (SurveyJsElementType.Ranking.Matches(type))
        {
            return Ranking;
        }

        if (SurveyJsElementType.MultipleText.Matches(type))
        {
            return MultipleText;
        }

        if (SurveyJsElementType.MatrixDropdown.Matches(type) || SurveyJsElementType.MatrixDynamic.Matches(type))
        {
            return MatrixCell;
        }

        if (SurveyJsElementType.FileUpload.Matches(type))
        {
            return File;
        }

        return Scalar;
    }

    internal static bool HasMatrixCheckboxCells(JsonElement element)
    {
        foreach ((_, _, var columnElement) in SurveyJsChoiceHelper.EnumerateMatrixColumns(element))
        {
            if (columnElement.ValueKind == JsonValueKind.Object &&
                string.Equals(
                    columnElement.GetStringProperty(SurveyJsPropertyNames.CellType),
                    SurveyJsElementType.Checkbox.Name,
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
