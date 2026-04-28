using FastEndpoints;
using FluentValidation;
using System.Text.Json;

namespace Endatix.Api.Endpoints.Forms;

/// <summary>
/// Validation rules for the <c>PartialUpdateFormRequest</c> class.
/// </summary>
public class PartialUpdateFormValidator : Validator<PartialUpdateFormRequest>
{
    /// <summary>
    /// Default constructor
    /// </summary>
    public PartialUpdateFormValidator()
    {
        RuleFor(x => x.FormId)
            .GreaterThan(0);

        RuleFor(x => x.Metadata)
            .Must(BeValidJson)
            .When(x => x.Metadata != null)
            .WithMessage("Metadata must be a valid JSON string.");
    }

    private static bool BeValidJson(string? json)
    {
        try
        {
            using var document = JsonDocument.Parse(json!);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
