namespace Endatix.Modules.Reporting.Features.FormSchema.FormSchema;

/// <summary>
/// One column in a compiled form schema derived from a SurveyJS form definition.
/// </summary>
internal sealed record FormSchemaColumn(
    string Key,
    FormSchemaColumnKind Kind,
    string Label,
    string DataType,
    string? SourceQuestion = null,
    string? ChoiceValue = null,
    IReadOnlyList<LoopSegment>? LoopPath = null,
    string? PanelName = null,
    int? PanelIndex = null,
    string? MatrixRowValue = null,
    string? MatrixColumnValue = null,
    IReadOnlyList<string>? MatrixColumnChoices = null);
