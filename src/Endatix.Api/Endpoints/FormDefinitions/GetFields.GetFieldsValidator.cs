using FastEndpoints;
using FluentValidation;

namespace Endatix.Api.Endpoints.FormDefinitions;

/// <summary>
/// Validation rules for the <c>GetFieldsRequest</c> class.
/// </summary>
public class GetFieldsValidator : Validator<GetFieldsRequest>
{
    /// <summary>
    /// Default constructor
    /// </summary>
    public GetFieldsValidator()
    {
        RuleFor(x => x.FormId)
            .GreaterThan(0);
    }
}
