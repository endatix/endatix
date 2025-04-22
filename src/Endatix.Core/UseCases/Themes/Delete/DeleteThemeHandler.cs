using Ardalis.GuardClauses;
using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;

namespace Endatix.Core.UseCases.Themes.Delete;

/// <summary>
/// Handler for deleting a theme.
/// </summary>
public class DeleteThemeHandler(
    IThemesRepository themesRepository,
    IRepository<Form> formsRepository
) : ICommandHandler<DeleteThemeCommand, Result<string>>
{
    /// <summary>
    /// Handles the deletion of a theme.
    /// </summary>
    /// <param name="request">The command containing the theme ID to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Success result if deleted, NotFound if theme doesn't exist.</returns>
    public async Task<Result<string>> Handle(DeleteThemeCommand request, CancellationToken cancellationToken)
    {
        Guard.Against.NegativeOrZero(request.ThemeId, nameof(request.ThemeId));

        try
        {
            var theme = await themesRepository.GetByIdAsync(request.ThemeId, cancellationToken);
            if (theme == null)
            {
                return Result.NotFound($"Theme with ID {request.ThemeId} not found");
            }

            // Check if there are forms using this theme
            var forms = await formsRepository.ListAsync(
                new FormSpecifications.ByThemeId(request.ThemeId),
                cancellationToken);

            if (forms.Any())
            {
                // Remove theme from all forms
                foreach (var form in forms)
                {
                    form.SetTheme(null);
                }
                await formsRepository.SaveChangesAsync(cancellationToken);
            }

            await themesRepository.DeleteAsync(theme, cancellationToken);
            await themesRepository.SaveChangesAsync(cancellationToken);

            return Result.Success(theme.Id.ToString());
        }
        catch (Exception ex)
        {
            return Result.Error($"Error deleting theme: {ex.Message}");
        }
    }
}