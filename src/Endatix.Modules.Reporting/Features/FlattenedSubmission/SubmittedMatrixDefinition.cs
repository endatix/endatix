using System.Text.Json;
using Endatix.Core.Entities;
using Endatix.Modules.Reporting.Features.FormSchema.Codebook;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;

namespace Endatix.Modules.Reporting.Features.FlattenedSubmission;

/// <summary>Column coverage alone cannot detect duplicate category meanings, including after an edit in place.</summary>
internal sealed class SubmittedMatrixDefinition
{
    private string? _lastValidated;

    public void Validate(FormDefinition? definition)
    {
        if (definition is null || definition.JsonData == _lastValidated)
        {
            return;
        }

        using var parsed = FormSchemaCompiler.ReadHistoricalDefinition(definition.JsonData);
        if (parsed?.RootElement is { ValueKind: JsonValueKind.Object } root)
        {
            FormSchemaCodebookBuilder.ValidateSubmittedMatrices(root);
        }

        _lastValidated = definition.JsonData;
    }
}
