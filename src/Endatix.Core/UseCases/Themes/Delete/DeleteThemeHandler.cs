using Ardalis.GuardClauses;
using Endatix.Core.Abstractions.Data;
using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using Microsoft.Extensions.Logging;

namespace Endatix.Core.UseCases.Themes.Delete;

/// <summary>
/// Handler for deleting a theme.
/// </summary>
public class DeleteThemeHandler(IRepository<Theme> themeRepository,
    IRepository<Form> formRepository,
    IUnitOfWork unitOfWork,
    ILogger<DeleteThemeHandler> logger, ManagedContentScope? scope)
    : ICommandHandler<DeleteThemeCommand, Result<string>>
{
    /// <summary>Constructs the ordinary operation without account standing enforcement.</summary>
    public DeleteThemeHandler(IRepository<Theme> themeRepository,
        IRepository<Form> formRepository,
        IUnitOfWork unitOfWork,
        ILogger<DeleteThemeHandler> logger)
        : this(themeRepository, formRepository, unitOfWork, logger, null) { }

    /// <summary>
    /// Handles the deletion of a theme with transaction support.
    /// </summary>
    /// <param name="request">The command containing the theme ID to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Success result if deleted, NotFound if theme doesn't exist.</returns>
    public async Task<Result<string>> Handle(DeleteThemeCommand request, CancellationToken cancellationToken)
    {
        Guard.Against.NegativeOrZero(request.ThemeId);

        if (scope is not null && await scope.TargetRefusalAsync<Theme, string>(themeRepository, new(request.ThemeId, $"Theme with ID {request.ThemeId} not found"), cancellationToken) is { } refusal)
        {
            return refusal;
        }

        return await DeleteAsync(request.ThemeId, cancellationToken);
    }

    private async Task<Result<string>> DeleteAsync(long themeId, CancellationToken cancellationToken)
    {
        try
        {
            await unitOfWork.BeginTransactionAsync(cancellationToken);

            return await DeleteInTransactionAsync(themeId, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to delete theme {ThemeId}", themeId);
            await unitOfWork.RollbackTransactionAsync(cancellationToken);
            return Result.Error("Failed to delete theme.");
        }
    }

    private async Task<Result<string>> DeleteInTransactionAsync(long themeId, CancellationToken cancellationToken)
    {
        var theme = await themeRepository.FindAsync(themeId, scope, cancellationToken);
        if (theme == null)
        {
            await unitOfWork.RollbackTransactionAsync(cancellationToken);
            return Result.NotFound($"Theme with ID {themeId} not found");
        }

        await ClearAssignedFormsAsync(themeId, cancellationToken);
        await themeRepository.DeleteAsync(theme, cancellationToken);

        // Single SaveChanges for all modifications
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await unitOfWork.CommitTransactionAsync(cancellationToken);

        return Result.Success(theme.Id.ToString());
    }

    private async Task ClearAssignedFormsAsync(long themeId, CancellationToken ct)
    {
        var assigned = new FormSpecifications.ByThemeId(themeId);
        assigned.InTenant(scope);
        foreach (var form in await formRepository.ListAsync(assigned, ct))
        {
            form.SetTheme(null);
        }
    }
}
