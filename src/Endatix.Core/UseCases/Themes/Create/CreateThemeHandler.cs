using Ardalis.GuardClauses;
using Endatix.Core.Abstractions;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.Themes.Create;

/// <summary>
/// Handler for creating a new theme.
/// </summary>
[DirectCallOnly]
public class CreateThemeHandler(IRepository<Theme> themesRepository,
    ITenantContext tenantContext) : ICommandHandler<CreateThemeCommand, Result<Theme>>
{
    private ContentQueryScope Selection { get; init; } = ContentQueryScope.Repository;

    /// <summary>
    /// Handles the creation of a new theme.
    /// </summary>
    /// <param name="request">The command containing theme creation data.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result containing the created theme.</returns>
    public async Task<Result<Theme>> Handle(CreateThemeCommand request, CancellationToken cancellationToken)
    {
        Guard.Against.NullOrEmpty(request.Name);
        var tenantId = tenantContext.TenantId;
        Guard.Against.NegativeOrZero(tenantId);

        var changes = new ThemeChanges(themesRepository, Selection);
        var change = new ThemeChanges.ThemeChange(null, request.Name, request.ThemeData);
        var data = await changes.DataAsync(change, cancellationToken);
        if (!data.IsSuccess)
        {
            return data.ToErrorResult<Theme>();
        }
        var theme = new Theme(tenantId, request.Name, request.Description);
        if (data.Value is { } json)
        {
            theme.UpdateJsonData(json);
        }
        await themesRepository.AddAsync(theme, cancellationToken);
        await themesRepository.SaveChangesAsync(cancellationToken);
        return Result<Theme>.Created(theme);
    }

    internal sealed class Registered(IRepository<Theme> themesRepository, ITenantContext tenantContext)
        : RegisteredContentHandler<CreateThemeCommand, Result<Theme>>(
            new CreateThemeHandler(themesRepository, tenantContext) { Selection = new ContentQueryScope(tenantContext) });
}
