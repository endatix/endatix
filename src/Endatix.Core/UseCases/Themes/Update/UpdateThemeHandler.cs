using Ardalis.GuardClauses;
using Endatix.Core.Abstractions;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.Themes.Update;

/// <summary>
/// Handler for updating a theme.
/// </summary>
[DirectCallOnly]
public class UpdateThemeHandler(IRepository<Theme> themesRepository) : ICommandHandler<UpdateThemeCommand, Result<Theme>>
{
    private ContentQueryScope Selection { get; init; } = ContentQueryScope.Repository;

    /// <summary>
    /// Handles the update of an existing theme.
    /// </summary>
    /// <param name="request">The command containing theme update data.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result containing the updated theme.</returns>
    public async Task<Result<Theme>> Handle(UpdateThemeCommand request, CancellationToken cancellationToken)
    {
        Guard.Against.NegativeOrZero(request.ThemeId);

        var changes = new ThemeChanges(themesRepository, Selection);
        var change = new ThemeChanges.ThemeChange(request.ThemeId, request.Name, request.ThemeData) { RequiresName = true };
        var edit = await changes.PrepareEditAsync(change, cancellationToken);
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

    internal sealed class Registered(IRepository<Theme> themesRepository, ContentQueryScope scope)
        : RegisteredContentHandler<UpdateThemeCommand, Result<Theme>>(
            new UpdateThemeHandler(themesRepository) { Selection = scope });
}
