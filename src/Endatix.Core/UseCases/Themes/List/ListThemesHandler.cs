using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;

namespace Endatix.Core.UseCases.Themes.List;

/// <summary>
/// Handler for retrieving all themes.
/// </summary>
public class ListThemesHandler(IThemesRepository themesRepository) : IQueryHandler<ListThemesQuery, Result<List<Theme>>>
{
    /// <summary>
    /// Handles the retrieval of all themes.
    /// </summary>
    /// <param name="request">The query containing optional pagination parameters.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result containing the list of themes.</returns>
    public async Task<Result<List<Theme>>> Handle(ListThemesQuery request, CancellationToken cancellationToken)
    {
        try
        {
            IEnumerable<Theme> themes;
            
            // Apply pagination if provided
            if (request.Page.HasValue && request.PageSize.HasValue)
            {
                var paginatedSpec = new ThemeSpecifications.Paginated(request.Page.Value, request.PageSize.Value);
                themes = await themesRepository.ListAsync(paginatedSpec, cancellationToken);
            }
            else
            {
                themes = await themesRepository.ListAsync(cancellationToken);
            }
            
            return Result<List<Theme>>.Success(themes.ToList());
        }
        catch (Exception ex)
        {
            return Result<List<Theme>>.Error($"Error retrieving themes: {ex.Message}");
        }
    }
} 