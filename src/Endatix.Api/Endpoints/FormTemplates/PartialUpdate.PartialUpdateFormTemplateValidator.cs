using FastEndpoints;
using FluentValidation;

namespace Endatix.Api.Endpoints.FormTemplates;

/// <summary>
/// Validation rules for the <c>PartialUpdateFormTemplateRequest</c> class.
/// </summary>
public class PartialUpdateFormTemplateValidator : Validator<PartialUpdateFormTemplateRequest>
{
    /// <summary>
    /// Default constructor
    /// </summary>
    public PartialUpdateFormTemplateValidator()
    {
        RuleFor(x => x.FormTemplateId)
            .GreaterThan(0);
    }
}
