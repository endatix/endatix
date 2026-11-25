using Ardalis.GuardClauses;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.Themes.PartialUpdate;

/// <summary>
/// Handler for partially updating a theme.
/// </summary>
public class PartialUpdateThemeHandler(IRepository<Theme> themeRepository, ManagedContentScope? scope)
    : ICommandHandler<PartialUpdateThemeCommand, Result<Theme>>
{
    /// <summary>Constructs the ordinary operation without account standing enforcement.</summary>
    public PartialUpdateThemeHandler(IRepository<Theme> themeRepository)
        : this(themeRepository, null) { }

    /// <summary>
    /// Handles the partial update of a theme.
    /// </summary>
    /// <param name="request">The command containing theme partial update data.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result containing the updated theme.</returns>
    public async Task<Result<Theme>> Handle(PartialUpdateThemeCommand request, CancellationToken cancellationToken)
    {
        Guard.Against.NegativeOrZero(request.ThemeId);

        var admission = new ThemeAdmission(themeRepository, scope);
        var change = new ThemeAdmission.ThemeChange(request.ThemeId, request.Name, request.ThemeData);
        var edit = await admission.PrepareEditAsync(change, cancellationToken);
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
}
