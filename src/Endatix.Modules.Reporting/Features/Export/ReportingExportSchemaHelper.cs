using Endatix.Core.Infrastructure.Result;
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

    internal static Result<T> MissingSchemaResult<T>() => Result<T>.Error(MissingSchemaMessage);

    internal static Result<T> InvalidSchemaArtifactsResult<T>() => Result<T>.Error(InvalidSchemaArtifactsMessage);
}
