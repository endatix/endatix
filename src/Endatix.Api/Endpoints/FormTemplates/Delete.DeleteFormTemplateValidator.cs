using FastEndpoints;
using FluentValidation;

namespace Endatix.Api.Endpoints.FormTemplates;

/// <summary>
/// Validation rules for the <c>DeleteFormTemplateRequest</c> class.
/// </summary>
public class DeleteFormTemplateValidator : Validator<DeleteFormTemplateRequest>
{
    /// <summary>
    /// Default constructor
    /// </summary>
    public DeleteFormTemplateValidator()
    {
        RuleFor(x => x.FormTemplateId)
            .GreaterThan(0);
    }
}
