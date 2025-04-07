using Ardalis.GuardClauses;
using Endatix.Core.Abstractions;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Models.Themes;
using Endatix.Core.Specifications;
using System.Text.Json;

namespace Endatix.Core.UseCases.Themes.Update;

/// <summary>
/// Handler for updating a theme.
/// </summary>
public class UpdateThemeHandler(IRepository<Theme> themesRepository) : ICommandHandler<UpdateThemeCommand, Result<Theme>>
{
    /// <summary>
    /// Handles the update of an existing theme.
    /// </summary>
    /// <param name="request">The command containing theme update data.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result containing the updated theme.</returns>
    public async Task<Result<Theme>> Handle(UpdateThemeCommand request, CancellationToken cancellationToken)
    {
        Guard.Against.NegativeOrZero(request.ThemeId, nameof(request.ThemeId));

        try
        {
            var theme = await themesRepository.GetByIdAsync(request.ThemeId, cancellationToken);
            if (theme == null)
            {
                return Result<Theme>.NotFound($"Theme with ID {request.ThemeId} not found");
            }

            // Check if another theme with the same name exists
            var existingTheme = await themesRepository.FirstOrDefaultAsync(
                new ThemeSpecifications.ByName(request.Name), 
                cancellationToken);

            if (existingTheme != null && existingTheme.Id != request.ThemeId)
            {
                return Result<Theme>.Error($"Another theme with the name '{request.Name}' already exists");
            }

            theme.UpdateName(request.Name);
            theme.UpdateDescription(request.Description);

            if (request.ThemeData != null)
            {
                string jsonData = JsonSerializer.Serialize(request.ThemeData);
                theme.UpdateJsonData(jsonData);
            }

            await themesRepository.UpdateAsync(theme, cancellationToken);
            await themesRepository.SaveChangesAsync(cancellationToken);

            return Result<Theme>.Success(theme);
        }
        catch (Exception ex)
        {
            return Result<Theme>.Error($"Error updating theme: {ex.Message}");
        }
    }
} 