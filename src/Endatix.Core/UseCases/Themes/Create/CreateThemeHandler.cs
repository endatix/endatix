using Ardalis.GuardClauses;
using Endatix.Core.Abstractions;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Models.Themes;
using Endatix.Core.Specifications;
using System.Text.Json;

namespace Endatix.Core.UseCases.Themes.Create;

/// <summary>
/// Handler for creating a new theme.
/// </summary>
public class CreateThemeHandler(
    IRepository<Theme> themesRepository,
    ITenantContext tenantContext
) : ICommandHandler<CreateThemeCommand, Result<Theme>>
{
    /// <summary>
    /// Handles the creation of a new theme.
    /// </summary>
    /// <param name="request">The command containing theme creation data.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result containing the created theme.</returns>
    public async Task<Result<Theme>> Handle(CreateThemeCommand request, CancellationToken cancellationToken)
    {
        Guard.Against.NullOrEmpty(request.Name, nameof(request.Name));
        var tenantId = tenantContext.TenantId;
        Guard.Against.NegativeOrZero(tenantId, "Current tenant ID");

        try
        {
            // Check if a theme with the same name already exists for this tenant
            var existingTheme = await themesRepository.FirstOrDefaultAsync(
                new ThemeSpecifications.ByName(request.Name),
                cancellationToken);

            if (existingTheme != null)
            {
                return Result<Theme>.Error($"A theme with the name '{request.Name}' already exists");
            }

            var jsonData = request.ThemeData != null
                ? JsonSerializer.Serialize(request.ThemeData)
                : JsonSerializer.Serialize(new ThemeData { ThemeName = request.Name });

            var theme = new Theme(tenantId, request.Name, request.Description, jsonData);

            await themesRepository.AddAsync(theme, cancellationToken);
            await themesRepository.SaveChangesAsync(cancellationToken);

            return Result<Theme>.Created(theme);
        }
        catch (Exception ex)
        {
            return Result<Theme>.Error($"Error creating theme: {ex.Message}");
        }
    }
}