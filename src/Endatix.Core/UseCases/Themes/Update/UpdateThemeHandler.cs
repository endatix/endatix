using Ardalis.GuardClauses;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.Themes.Update;

/// <summary>
/// Handler for updating a theme.
/// </summary>
public class UpdateThemeHandler(IRepository<Theme> themesRepository, ManagedContentScope? scope)
    : ICommandHandler<UpdateThemeCommand, Result<Theme>>
{
    /// <summary>Constructs the ordinary operation without account standing enforcement.</summary>
    public UpdateThemeHandler(IRepository<Theme> themesRepository)
        : this(themesRepository, null) { }

    /// <summary>
    /// Handles the update of an existing theme.
    /// </summary>
    /// <param name="request">The command containing theme update data.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result containing the updated theme.</returns>
    public async Task<Result<Theme>> Handle(UpdateThemeCommand request, CancellationToken cancellationToken)
    {
        Guard.Against.NegativeOrZero(request.ThemeId);

        var admission = new ThemeAdmission(themesRepository, scope);
        var change = new ThemeAdmission.ThemeChange(request.ThemeId, request.Name, request.ThemeData);
        var edit = await admission.PrepareEditAsync(change, cancellationToken);
        if (!edit.IsSuccess)
        {
            return edit.ToErrorResult<Theme>();
        }
        var theme = edit.Value.Target;
        theme.UpdateName(request.Name);
        theme.UpdateDescription(request.Description);
        if (edit.Value.Data is { } json)
        {
            theme.UpdateJsonData(json);
        }
        await themesRepository.UpdateAsync(theme, cancellationToken);
        await themesRepository.SaveChangesAsync(cancellationToken);
        return Result.Success(theme);
    }
}
