namespace Endatix.Modules.Reporting.Domain.SurveyJs;

/// <summary>
/// One export column derived from a SurveyJS form definition.
/// </summary>
internal sealed record CodebookColumnDefinition(
    string Key,
    CodebookColumnKind Kind,
    string Label,
    string DataType,
    string? SourceQuestion = null,
    string? ChoiceValue = null,
    IReadOnlyList<LoopSegment>? LoopPath = null,
    string? PanelName = null,
    int? PanelIndex = null,
    string? MatrixRowValue = null);
