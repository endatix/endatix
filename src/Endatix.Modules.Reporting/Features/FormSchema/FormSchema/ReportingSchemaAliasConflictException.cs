namespace Endatix.Modules.Reporting.Features.FormSchema.FormSchema;

internal sealed class ReportingSchemaAliasConflictException(string detail)
    : InvalidOperationException("A published reporting alias has conflicting meanings.")
{
    public const string CODE = "ReportingSchemaAliasConflict";

    public string Detail { get; } = detail;

    internal ReportingSchemaAliasConflictException(string alias, FormSchemaColumn previous, FormSchemaColumn requested)
        : this($"Reporting alias '{alias}' already exports {Meaning(previous)} and cannot also export {Meaning(requested)}. " +
               "Change the conflicting question name or choice value and compile again. The previous reporting schema was retained.")
    {
    }

    private static string Meaning(FormSchemaColumn column) => column.ChoiceValue is { } choice
        ? $"{column.Kind} for choice '{choice}' of question '{column.SourceQuestion}' (key '{column.Key}')"
        : $"{column.Kind} for '{column.SourceQuestion ?? column.Key}' (key '{column.Key}')";
}
