namespace Endatix.Modules.Reporting.Features.FormSchema.FormSchema;

/// <summary>
/// One level in a choice-driven <c>paneldynamic</c> loop path.
/// </summary>
internal sealed record LoopSegment(
    string PanelValueName,
    string PropertyName,
    string ChoiceValue);
