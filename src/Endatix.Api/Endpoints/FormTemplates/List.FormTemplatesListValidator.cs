using Endatix.Api.Common;
using FastEndpoints;
using FluentValidation;

namespace Endatix.Api.Endpoints.FormTemplates;

/// <summary>
/// Validation rules for the <c>FormTemplatesListRequest</c> class.
/// </summary>
public class FormTemplatesListValidator : Validator<FormTemplatesListRequest>
{
    /// <summary>
    /// Default constructor
    /// </summary>
    public FormTemplatesListValidator()
    {
        Include(new PagedRequestValidator());
    }
}
