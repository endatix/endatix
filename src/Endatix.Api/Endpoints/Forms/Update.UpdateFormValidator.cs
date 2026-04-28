using FastEndpoints;
using FluentValidation;
using Endatix.Infrastructure.Data.Config;
using System.Text.Json;

namespace Endatix.Api.Endpoints.Forms;

/// <summary>
/// Validation rules for the <c>UpdateFormRequest</c> class.
/// </summary>
public class UpdateFormValidator : Validator<UpdateFormRequest>
{
    /// <summary>
    /// Default constructor
    /// </summary>
    public UpdateFormValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MinimumLength(DataSchemaConstants.MIN_NAME_LENGTH)
            .MaximumLength(DataSchemaConstants.MAX_NAME_LENGTH);

        RuleFor(x => x.IsEnabled)
            .NotEmpty();

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
