namespace Endatix.Modules.Reporting.Features.FormSchema.FormSchema;

internal enum FormSchemaColumnKind
{
    Simple,
    Calculated,
    ChoiceIndicator,
    CheckboxOtherText,
    PanelDynamicIndex,
    NestedLoop,
    LoopSource,
    MatrixRow,
    MatrixCell,
    RankingChoice,
    MultipleTextItem,
    FileUpload,

    /// <summary>An audience value frozen on the response, read from its personalization snapshot, not its answers.</summary>
    Personalization,
}
