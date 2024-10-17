using FastEndpoints;
using FluentValidation;

namespace Endatix.Api.Endpoints.Assistant;

/// <summary>
/// Validation rules for the <c>DefineFormRequest</c> class.
/// </summary>
public class DefineFormValidator : Validator<DefineFormRequest>
{
    /// <summary>
    /// Default constructor
    /// </summary>
    public DefineFormValidator()
    {
        RuleFor(x => x.Prompt)
            .NotEmpty()
            .MaximumLength(1000);

        RuleFor(x => x.Definition)
            .MaximumLength(10000)
            .When(x => x.Definition != null);

        RuleFor(x => x.AssistantId)
            .NotEmpty()
            .When(x => x.AssistantId != null);

        RuleFor(x => x.ThreadId)
            .NotEmpty()
            .When(x => x.ThreadId != null);
    }
}
