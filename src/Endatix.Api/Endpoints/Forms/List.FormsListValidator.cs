using Endatix.Api.Common;
using FastEndpoints;
using FluentValidation;

namespace Endatix.Api.Endpoints.Forms;

/// <summary>
/// Validation rules for the <c>FormsListRequest</c> class.
/// </summary>
public class FormsListValidator : Validator<FormsListRequest>
{
    private static readonly Dictionary<string, Type> _filterableFields = new()
    {
        { "id", typeof(long) },
        { "createdAt", typeof(DateTime) },
        { "updatedAt", typeof(DateTime) },
        { "isEnabled", typeof(bool) },
        { "themeId", typeof(long) },
        { "activeDefinitionId", typeof(long) },
        { "name", typeof(string) },
        { "description", typeof(string) }
    };
    /// <summary>
    /// Default constructor
    /// </summary>
    public FormsListValidator()
    {
        Include(new PagedRequestValidator());
        Include(new FilteredRequestValidator(_filterableFields));
    }
}
