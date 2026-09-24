using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using Endatix.Core.Infrastructure.Paging;
using Endatix.Core.Specifications.Parameters;

namespace Endatix.Core.UseCases.Themes.List;

/// <summary>
/// Handler for retrieving themes as a paged envelope.
/// </summary>
public class ListThemesHandler(IRepository<Theme> themeRepository)
    : IQueryHandler<ListThemesQuery, Result<Paged<Theme>>>
{
    /// <inheritdoc />
    public async Task<Result<Paged<Theme>>> Handle(
        ListThemesQuery request,
        CancellationToken cancellationToken)
    {
        var pagingParams = new PagingParameters(
            request.Page,
            request.PageSize);

        var countSpec = new ThemeSpecifications.ListFilter(
            request.Created,
            request.Modified);
        var totalRecords = await themeRepository.CountAsync(countSpec, cancellationToken);

        var window = new PageRequest(pagingParams.Page, pagingParams.PageSize).ForTotal(totalRecords);

        IReadOnlyList<Theme> items = [];
        if (totalRecords > 0)
        {
            var spec = new ThemeSpecifications.Paginated(
                new PagingParameters(window.Page, window.PageSize),
                request.SortBy,
                request.SortDescending,
                request.Created,
                request.Modified);
            items = [.. await themeRepository.ListAsync(spec, cancellationToken)];
        }

        return Result.Success(window.ToPaged(totalRecords, items));
    }
}
