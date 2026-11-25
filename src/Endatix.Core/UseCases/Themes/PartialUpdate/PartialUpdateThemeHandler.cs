using Ardalis.GuardClauses;
using Endatix.Core.Abstractions;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.Themes.PartialUpdate;

/// <summary>
/// Handler for partially updating a theme.
/// </summary>
[DirectCallOnly]
public class PartialUpdateThemeHandler(IRepository<Theme> themeRepository) : ICommandHandler<PartialUpdateThemeCommand, Result<Theme>>
{
    private ContentQueryScope Selection { get; init; } = ContentQueryScope.Repository;

    /// <summary>
    /// Handles the partial update of a theme.
    /// </summary>
    /// <param name="request">The command containing theme partial update data.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result containing the updated theme.</returns>
    public async Task<Result<Theme>> Handle(PartialUpdateThemeCommand request, CancellationToken cancellationToken)
    {
        Guard.Against.NegativeOrZero(request.ThemeId);

        var changes = new ThemeChanges(themeRepository, Selection);
        var change = new ThemeChanges.ThemeChange(request.ThemeId, request.Name, request.ThemeData);
        var edit = await changes.PrepareEditAsync(change, cancellationToken);
        if (!edit.IsSuccess)
        {
            return edit.ToErrorResult<Theme>();
        }
        var theme = edit.Value.Target;
        if (!string.IsNullOrEmpty(request.Name))
        {
            theme.UpdateName(request.Name);
        }
        if (request.Description is not null)
        {
            theme.UpdateDescription(request.Description);
        }
        if (edit.Value.Data is { } json)
        {
            theme.UpdateJsonData(json);
        }
        await themeRepository.UpdateAsync(theme, cancellationToken);
        await themeRepository.SaveChangesAsync(cancellationToken);
        return Result.Success(theme);
    }

    internal sealed class Registered(IRepository<Theme> themeRepository, ContentQueryScope scope)
        : RegisteredContentHandler<PartialUpdateThemeCommand, Result<Theme>>(
            new PartialUpdateThemeHandler(themeRepository) { Selection = scope });
}
