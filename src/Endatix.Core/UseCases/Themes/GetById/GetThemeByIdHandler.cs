using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.Themes.GetById;

/// <summary>
/// Handler for retrieving a theme by ID.
/// </summary>
public class GetThemeByIdHandler(IRepository<Theme> themeRepository) : IQueryHandler<GetThemeByIdQuery, Result<Theme>>
{
    /// <summary>
    /// Handles the retrieval of a theme by ID.
    /// </summary>
    /// <param name="request">The query containing the theme ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result containing the theme if found, or an error if not found.</returns>
    public async Task<Result<Theme>> Handle(GetThemeByIdQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var theme = await themeRepository.GetByIdAsync(request.ThemeId, cancellationToken);

            if (theme == null)
            {
                return Result<Theme>.NotFound($"Theme not found.");
            }

            return Result<Theme>.Success(theme);
        }
        catch (Exception ex)
        {
            return Result<Theme>.Error($"Error retrieving theme: {ex.Message}");
        }
    }
}