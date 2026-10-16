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
    /// <summary>Single-select choice question (one column / category id).</summary>
    public static readonly FormSchemaCodebookExportShape Categorical = new("categorical");
    /// <summary>Multi-select choice question (one indicator column per choice).</summary>
    public static readonly FormSchemaCodebookExportShape MultipleResponse = new("multiple_response");
    public static readonly FormSchemaCodebookExportShape Ranking = new("ranking");
    public static readonly FormSchemaCodebookExportShape MultipleText = new("multiple_text");
    public static readonly FormSchemaCodebookExportShape MatrixCell = new("matrix_cell");
    public static readonly FormSchemaCodebookExportShape File = new("file");
    public static readonly FormSchemaCodebookExportShape Scalar = new("scalar");
    /// <summary>Calculated value saved to the survey results (one column, any value).</summary>
    public static readonly FormSchemaCodebookExportShape Calculated = new("calculated");
    /// <summary>Audience value frozen on the response (one column, typed by the audience property).</summary>
    public static readonly FormSchemaCodebookExportShape Personalization = new("personalization");
    /// <summary>Submitter profile field captured with the response (one text column).</summary>
    public static readonly FormSchemaCodebookExportShape SubmitterProfile = new("submitter_profile");

    // Declared after the shapes it holds: static fields initialize in declaration order.
    private static readonly Dictionary<FormSchemaColumnKind, FormSchemaCodebookExportShape> _byColumnKind = new()
    {
        [FormSchemaColumnKind.Personalization] = Personalization,
        [FormSchemaColumnKind.SubmitterProfile] = SubmitterProfile,
        [FormSchemaColumnKind.ChoiceIndicator] = MultipleResponse,
        [FormSchemaColumnKind.CheckboxOtherText] = MultipleResponse,
        [FormSchemaColumnKind.MatrixRow] = CategoricalArray,
        [FormSchemaColumnKind.MatrixCell] = MatrixCell,
        [FormSchemaColumnKind.RankingChoice] = Ranking,
        [FormSchemaColumnKind.MultipleTextItem] = MultipleText,
        [FormSchemaColumnKind.FileUpload] = File,
        [FormSchemaColumnKind.Calculated] = Calculated,
    };

    public static FormSchemaCodebookExportShape FromColumnKind(FormSchemaColumnKind kind) =>
        _byColumnKind.GetValueOrDefault(kind, Scalar);

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

        if (questionElement.IsMultiSelectBaseSelect())
        {
            return MultipleResponse;
        }

        if (questionElement.IsSingleSelectBaseSelect())
        {
            return Categorical;
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
