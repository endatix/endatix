using FastEndpoints;
using FluentValidation;
using Endatix.Infrastructure.Data.Config;

namespace Endatix.Api.Endpoints.CustomQuestions;

/// <summary>
/// Validation rules for the <c>CreateCustomQuestionRequest</c> class.
/// </summary>
public class CreateCustomQuestionValidator : Validator<CreateCustomQuestionRequest>
{
    /// <summary>
    /// Default constructor
    /// </summary>
    public CreateCustomQuestionValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MinimumLength(DataSchemaConstants.MIN_NAME_LENGTH)
            .MaximumLength(DataSchemaConstants.MAX_NAME_LENGTH);

        RuleFor(x => x.JsonData)
            .NotEmpty()
            .MinimumLength(DataSchemaConstants.MIN_JSON_LENGTH);
    }
} 