using Endatix.Api.Common;
using Endatix.Core.UseCases.Themes.List;
using FastEndpoints;

namespace Endatix.Api.Endpoints.Themes;

public class ListValidator : Validator<ListRequest>
{
    public ListValidator()
    {
        Include(new PageableRequestValidator());
        Include(new SortableRequestValidator<ThemeListSortBy>());
        this.RuleForCalendarDayRange(x => x.CreatedFrom, x => x.CreatedTo, "CreatedFrom");
        this.RuleForCalendarDayRange(x => x.ModifiedFrom, x => x.ModifiedTo, "ModifiedFrom");
    }
}
