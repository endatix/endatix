using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using Endatix.Core.Specifications.Parameters;

namespace Endatix.Core.UseCases.Themes.List;

/// <summary>
/// Handler for retrieving all themes.
/// </summary>
public class ListThemesHandler(IThemesRepository themesRepository)
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
        try
        {
            var pagingParams = new PagingParameters(
                request.Page,
                request.PageSize);

            var spec = new ThemeSpecifications.Paginated(pagingParams);
            IEnumerable<Theme> themes = await themesRepository.ListAsync(spec, cancellationToken);

            return Result.Success(themes);
        }
        catch (Exception ex)
        {
            return Result.Error($"Error retrieving themes: {ex.Message}");
        }
    }
}