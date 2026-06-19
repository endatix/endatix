using Endatix.Api.Common;
using FastEndpoints;
using FluentValidation;

namespace Endatix.Api.Endpoints.FormTemplates;

/// <summary>
/// Validation rules for the <c>FormTemplatesListRequest</c> class.
/// </summary>
public class FormTemplatesListValidator : Validator<FormTemplatesListRequest>
{
    private static readonly Dictionary<string, Type> _filterableFields = new()
    {
        { "folderId", typeof(long?) }
    };

    /// <summary>
    /// Default constructor
    /// </summary>
    public FormTemplatesListValidator()
    {
        Include(new PageableRequestValidator());
        Include(new FilteredRequestValidator(_filterableFields));
    }
}
