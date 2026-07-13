namespace Endatix.Modules.Reporting.Features.FormSchema.FormSchema;

/// <summary>
/// JSON property names for persisted <see cref="MergedFormSchema"/> and domain <see cref="Endatix.Modules.Reporting.Domain.FormSchema.FlatteningMap"/>.
/// </summary>
/// <remarks>
/// These are the property names used in the JSON schema for the form schema.
/// </remarks>
internal static class FormSchemaPropertyNames
{
    public const string Key = "key";
    public const string Kind = "kind";
    public const string Label = "label";
    public const string DataType = "dataType";
    public const string SourceQuestion = "sourceQuestion";
    public const string ChoiceValue = "choiceValue";
    public const string PanelName = "panelName";
    public const string PanelIndex = "panelIndex";
    public const string MatrixRowValue = "matrixRowValue";
    public const string MatrixColumnValue = "matrixColumnValue";
    public const string MatrixColumnChoices = "matrixColumnChoices";
    public const string LoopPath = "loopPath";
    public const string PanelValueName = "panelValueName";
    public const string PropertyName = "propertyName";
}
