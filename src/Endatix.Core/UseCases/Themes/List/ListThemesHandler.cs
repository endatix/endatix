using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using Endatix.Core.Specifications.Parameters;

namespace Endatix.Core.UseCases.Themes.List;

/// <summary>
/// Handler for retrieving all themes.
/// </summary>
public class ListThemesHandler(IRepository<Theme> themeRepository)
    : IQueryHandler<ListThemesQuery, Result<IEnumerable<Theme>>>
{
    /// <summary>
    /// Handles the retrieval of all themes.
    /// </summary>
    /// <param name="request">The query containing optional pagination parameters.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result containing the list of themes.</returns>
    public async Task<Result<IEnumerable<Theme>>> Handle(ListThemesQuery request, CancellationToken cancellationToken)
    {
        var pagingParams = new PagingParameters(
                 request.Page,
                 request.PageSize);

        var spec = new ThemeSpecifications.Paginated(pagingParams);
        IEnumerable<Theme> themes = await themeRepository.ListAsync(spec, cancellationToken);

        return Result<IEnumerable<Theme>>.Success(themes);
    }
}