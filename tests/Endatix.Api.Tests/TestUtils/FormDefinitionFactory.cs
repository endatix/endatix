using Endatix.Core.Entities;

namespace Endatix.Api.Tests.TestUtils;

/// <summary>
/// Factory class for creating FormDefinition instances for testing purposes.
/// Uses reflection to set FormId directly, ensuring deterministic values without relying on normal entity relationships.
/// </summary>
public static class FormDefinitionFactory
{
    public static FormDefinition CreateForTesting(bool isDraft = false, string? jsonData = null, long? formId = default, long? formDefinitionId = default)
    {
        var formDefinition = new FormDefinition(SampleData.TENANT_ID, isDraft, jsonData);
        if (formId.HasValue)
        {
            typeof(FormDefinition)
                .GetProperty(nameof(FormDefinition.FormId))
                ?.SetValue(formDefinition, formId);
        }
        if (formDefinitionId.HasValue)
        {
            typeof(FormDefinition)
                .GetProperty(nameof(FormDefinition.Id))
                ?.SetValue(formDefinition, formDefinitionId);
        }

        return formDefinition;
    }
}