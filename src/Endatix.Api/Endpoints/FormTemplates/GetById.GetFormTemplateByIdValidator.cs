using FastEndpoints;
using FluentValidation;

namespace Endatix.Api.Endpoints.FormTemplates;

/// <summary>
/// Validation rules for the <c>GetFormTemplateByIdRequest</c> class.
/// </summary>
public class GetFormTemplateByIdValidator : Validator<GetFormTemplateByIdRequest>
{
    /// <summary>
    /// Default constructor
    /// </summary>
    public GetFormTemplateByIdValidator()
    {
        RuleFor(x => x.FormTemplateId)
            .GreaterThan(0);
    }
}
