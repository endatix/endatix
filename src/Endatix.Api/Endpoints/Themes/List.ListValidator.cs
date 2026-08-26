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
        Include(new CreatedRangeRequestValidator());
        Include(new ModifiedRangeRequestValidator());
    }
}
