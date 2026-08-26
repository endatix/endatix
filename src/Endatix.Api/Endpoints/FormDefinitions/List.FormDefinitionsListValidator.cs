using Endatix.Api.Common;
using Endatix.Core.UseCases.FormDefinitions.List;
using FastEndpoints;
using FluentValidation;

namespace Endatix.Api.Endpoints.FormDefinitions;

/// <summary>
/// Validation rules for the <c>FormDefinitionsListRequest</c> class.
/// </summary>
public class FormDefinitionsListValidator : Validator<FormDefinitionsListRequest>
{
    /// <summary>
    /// Default constructor
    /// </summary>
    public FormDefinitionsListValidator()
    {
        Include(new PageableRequestValidator());
        Include(new SortableRequestValidator<FormDefinitionListSortBy>());
        this.RuleForCalendarDayRange(x => x.CreatedFrom, x => x.CreatedTo, "CreatedFrom");
        this.RuleForCalendarDayRange(x => x.ModifiedFrom, x => x.ModifiedTo, "ModifiedFrom");

        RuleFor(x => x.FormId)
            .GreaterThan(0);
    }
}
