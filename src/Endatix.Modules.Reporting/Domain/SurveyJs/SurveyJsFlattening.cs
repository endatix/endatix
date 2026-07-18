namespace Endatix.Modules.Reporting.Domain.SurveyJs;

/// <summary>
/// How a SurveyJS element maps to flattened form schema columns.
/// </summary>
internal enum SurveyJsFlattening
{
    /// <summary>Display-only or structural; no data column.</summary>
    None,

    /// <summary>One column per question name.</summary>
    Simple,

    /// <summary>One indicator column per choice (multi-select: checkbox, tagbox, multi imagepicker).</summary>
    ChoiceIndicators,

    /// <summary>One numeric rank column per choice.</summary>
    Ranking,

    /// <summary>One column per matrix row.</summary>
    Matrix,

    /// <summary>Indexed columns per panel instance (standalone paneldynamic).</summary>
    PanelDynamic,

    /// <summary>One column per multipletext item name.</summary>
    MultipleText,

    /// <summary>Single column with joined file metadata (URLs/names).</summary>
    File,
}
