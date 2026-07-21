using Endatix.Core.Infrastructure.Result;
using Endatix.Modules.Reporting.Features.FormSchema;
using FormSchemaEntity = Endatix.Modules.Reporting.Domain.FormSchema;

namespace Endatix.Modules.Reporting.Features.Export;

internal static class ReportingExportSchemaHelper
{
    internal const string MissingSchemaMessage =
        "Form schema has not been compiled for this form. Save or publish the form definition to trigger compilation.";

    internal const string InvalidSchemaArtifactsMessage =
        "Form schema artifacts are incomplete or invalid. Save or publish the form definition to recompile the schema.";

    internal static bool HasValidSchemaArtifacts(FormSchemaEntity schema) =>
        !string.IsNullOrWhiteSpace(schema.FlatteningMap) && !string.IsNullOrWhiteSpace(schema.Codebook);

    internal static Result<T> MissingSchemaResult<T>() => Result<T>.Conflict(MissingSchemaMessage);

    internal static Result<T> InvalidSchemaArtifactsResult<T>() => Result<T>.Conflict(InvalidSchemaArtifactsMessage);

    internal static bool IsLocaleAllowed(string localesJson, string? locale) =>
        string.IsNullOrWhiteSpace(locale) ||
        FormSchemaLocales.Contains(localesJson, locale);

    internal static string ResolveLocaleOrDefault(string? locale) =>
        string.IsNullOrWhiteSpace(locale) ? "default" : locale.Trim();

    internal static Result<T> InvalidLocaleResult<T>(string localesJson, string locale)
    {
        var available = string.Join(", ", FormSchemaLocales.Parse(localesJson));
        return Result<T>.Invalid(new ValidationError(
            $"Locale '{locale}' is not available for this form. Available locales: {available}."));
    }
}
