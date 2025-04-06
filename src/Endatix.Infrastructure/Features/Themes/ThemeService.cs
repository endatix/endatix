using Ardalis.GuardClauses;
using Endatix.Core.Abstractions;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Entities;
using Exceptions = Endatix.Core.Exceptions;
using Endatix.Core.Features.Themes;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using System.Text.Json;

namespace Endatix.Infrastructure.Features.Themes;

public sealed class ThemeService : IThemeService
{
    private readonly IThemesRepository _themesRepository;
    private readonly IRepository<Form> _formsRepository;
    private readonly ITenantContext _tenantContext;

    public ThemeService(
        IThemesRepository themesRepository, 
        IRepository<Form> formsRepository,
        ITenantContext tenantContext)
    {
        _themesRepository = themesRepository;
        _formsRepository = formsRepository;
        _tenantContext = tenantContext;
    }

    public async Task<Result<Theme>> CreateThemeAsync(
        string name, 
        string? description = null, 
        ThemeData? themeData = null, 
        CancellationToken cancellationToken = default)
    {
        Guard.Against.NullOrEmpty(name, nameof(name));
        var tenantId = _tenantContext.TenantId;
        Guard.Against.NegativeOrZero(tenantId, "Current tenant ID");

        try
        {
            // Check if a theme with the same name already exists for this tenant
            var existingTheme = await _themesRepository.FirstOrDefaultAsync(
                new ThemeSpecifications.ByName(name), 
                cancellationToken);

            if (existingTheme != null)
            {
                return Result<Theme>.Error($"A theme with the name '{name}' already exists");
            }

            var jsonData = themeData != null 
                ? JsonSerializer.Serialize(themeData) 
                : JsonSerializer.Serialize(new ThemeData { ThemeName = name });

            var theme = new Theme(tenantId, name, description, jsonData);
            
            await _themesRepository.AddAsync(theme, cancellationToken);
            await _themesRepository.SaveChangesAsync(cancellationToken);

            return Result<Theme>.Success(theme);
        }
        catch (Exception ex)
        {
            return Result<Theme>.Error($"Error creating theme: {ex.Message}");
        }
    }

    public async Task<Result<Theme>> GetThemeByIdAsync(long themeId, CancellationToken cancellationToken = default)
    {
        Guard.Against.NegativeOrZero(themeId, nameof(themeId));

        try
        {
            var theme = await _themesRepository.FirstOrDefaultAsync(
                new ThemeSpecifications.ByIdWithForms(themeId), 
                cancellationToken);

            if (theme == null)
            {
                return Result<Theme>.NotFound($"Theme with ID {themeId} not found");
            }

            return Result<Theme>.Success(theme);
        }
        catch (Exception ex)
        {
            return Result<Theme>.Error($"Error retrieving theme: {ex.Message}");
        }
    }

    public async Task<Result<List<Theme>>> GetThemesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var themes = await _themesRepository.ListAsync(cancellationToken);
            return Result<List<Theme>>.Success(themes.ToList());
        }
        catch (Exception ex)
        {
            return Result<List<Theme>>.Error($"Error retrieving themes: {ex.Message}");
        }
    }

    public async Task<Result<Theme>> UpdateThemeAsync(
        long themeId, 
        string? name = null, 
        string? description = null, 
        ThemeData? themeData = null, 
        CancellationToken cancellationToken = default)
    {
        Guard.Against.NegativeOrZero(themeId, nameof(themeId));

        try
        {
            var theme = await _themesRepository.GetByIdAsync(themeId, cancellationToken);
            if (theme == null)
            {
                return Result<Theme>.NotFound($"Theme with ID {themeId} not found");
            }

            // Update name if provided
            if (!string.IsNullOrEmpty(name))
            {
                // Check if another theme with the same name exists
                var existingTheme = await _themesRepository.FirstOrDefaultAsync(
                    new ThemeSpecifications.ByName(name), 
                    cancellationToken);

                if (existingTheme != null && existingTheme.Id != themeId)
                {
                    return Result<Theme>.Error($"Another theme with the name '{name}' already exists");
                }

                theme.UpdateName(name);
            }

            // Update description if provided
            if (description != null)
            {
                theme.UpdateDescription(description);
            }

            // Update theme data if provided
            if (themeData != null)
            {
                string jsonData = JsonSerializer.Serialize(themeData);
                theme.UpdateJsonData(jsonData);
            }

            await _themesRepository.UpdateAsync(theme, cancellationToken);
            await _themesRepository.SaveChangesAsync(cancellationToken);

            return Result<Theme>.Success(theme);
        }
        catch (Exception ex)
        {
            return Result<Theme>.Error($"Error updating theme: {ex.Message}");
        }
    }

    public async Task<Result> DeleteThemeAsync(long themeId, CancellationToken cancellationToken = default)
    {
        Guard.Against.NegativeOrZero(themeId, nameof(themeId));

        try
        {
            var theme = await _themesRepository.GetByIdAsync(themeId, cancellationToken);
            if (theme == null)
            {
                return Result.NotFound($"Theme with ID {themeId} not found");
            }

            // Check if there are forms using this theme
            var forms = await _formsRepository.ListAsync(
                new FormSpecifications.ByThemeId(themeId), 
                cancellationToken);

            if (forms.Any())
            {
                // Remove theme from all forms
                foreach (var form in forms)
                {
                    form.SetTheme(null);
                }
                await _formsRepository.SaveChangesAsync(cancellationToken);
            }

            await _themesRepository.DeleteAsync(theme, cancellationToken);
            await _themesRepository.SaveChangesAsync(cancellationToken);

            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Error($"Error deleting theme: {ex.Message}");
        }
    }

    public async Task<Result<List<Form>>> GetFormsByThemeIdAsync(long themeId, CancellationToken cancellationToken = default)
    {
        Guard.Against.NegativeOrZero(themeId, nameof(themeId));

        try
        {
            var forms = await _formsRepository.ListAsync(
                new FormSpecifications.ByThemeId(themeId), 
                cancellationToken);

            return Result<List<Form>>.Success(forms.ToList());
        }
        catch (Exception ex)
        {
            return Result<List<Form>>.Error($"Error retrieving forms: {ex.Message}");
        }
    }

    public async Task<Result<Form>> AssignThemeToFormAsync(long formId, long themeId, CancellationToken cancellationToken = default)
    {
        Guard.Against.NegativeOrZero(formId, nameof(formId));
        Guard.Against.NegativeOrZero(themeId, nameof(themeId));

        try
        {
            return Result<Form>.Success(
                await _themesRepository.AssignThemeToFormAsync(formId, themeId, cancellationToken));
        }
        catch (Exceptions.NotFoundException ex)
        {
            return Result<Form>.NotFound(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return Result<Form>.Error(ex.Message);
        }
        catch (Exception ex)
        {
            return Result<Form>.Error($"Error assigning theme to form: {ex.Message}");
        }
    }

    public async Task<Result<Form>> RemoveThemeFromFormAsync(long formId, CancellationToken cancellationToken = default)
    {
        Guard.Against.NegativeOrZero(formId, nameof(formId));

        try
        {
            return Result<Form>.Success(
                await _themesRepository.RemoveThemeFromFormAsync(formId, cancellationToken));
        }
        catch (Exceptions.NotFoundException ex)
        {
            return Result<Form>.NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            return Result<Form>.Error($"Error removing theme from form: {ex.Message}");
        }
    }
} 