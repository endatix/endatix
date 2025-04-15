using Endatix.Api.Common;
using FastEndpoints;

namespace Endatix.Api.Endpoints.Themes.List;
public class ListValidator : Validator<ListRequest>
{
    public ListValidator()
    {
        Include(new PagedRequestValidator());
    }
}