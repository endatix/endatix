using Ardalis.GuardClauses;
using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Data;
using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Endatix.Core.UseCases.Themes.Delete;

/// <summary>
/// Handler for deleting a theme.
/// </summary>
[DirectCallOnly]
public class DeleteThemeHandler : ICommandHandler<DeleteThemeCommand, Result<string>>
{
    private readonly ThemeRemoval _removal;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger _logger;

    /// <summary>Uses the supplied repositories for direct calls.</summary>
    public DeleteThemeHandler(IRepository<Theme> themeRepository, IRepository<Form> formRepository,
        IUnitOfWork unitOfWork, ILogger<DeleteThemeHandler> logger)
        : this(new ThemeRemoval(themeRepository, formRepository, ContentQueryScope.Repository), unitOfWork, logger) { }

    private DeleteThemeHandler(ThemeRemoval removal, IUnitOfWork unitOfWork, ILogger logger) =>
        (_removal, _unitOfWork, _logger) = (removal, unitOfWork, logger);

    /// <summary>
    /// Handles the deletion of a theme with transaction support.
    /// </summary>
    /// <param name="request">The command containing the theme ID to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Success result if deleted, NotFound if theme doesn't exist.</returns>
    public async Task<Result<string>> Handle(DeleteThemeCommand request, CancellationToken cancellationToken)
    {
        Guard.Against.NegativeOrZero(request.ThemeId);

        return await DeleteAsync(request.ThemeId, cancellationToken);
    }

    private async Task<Result<string>> DeleteAsync(long themeId, CancellationToken cancellationToken)
    {
        try
        {
            await _unitOfWork.BeginTransactionAsync(cancellationToken);

            return await DeleteInTransactionAsync(themeId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete theme {ThemeId}", themeId);
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            return Result.Error("Failed to delete theme.");
        }
    }

    private async Task<Result<string>> DeleteInTransactionAsync(long themeId, CancellationToken cancellationToken)
    {
        if (await _removal.RemoveAsync(themeId, cancellationToken) is not long removedId)
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            return Result.NotFound($"Theme with ID {themeId} not found");
        }

        // Single SaveChanges for all modifications
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _unitOfWork.CommitTransactionAsync(cancellationToken);

        return Result.Success(removedId.ToString());
    }

    internal sealed class Registered(ThemeRemoval removal, IUnitOfWork unitOfWork, [FromKeyedServices(typeof(DeleteThemeHandler))] ILogger logger)
        : RegisteredContentHandler<DeleteThemeCommand, Result<string>>(
            new DeleteThemeHandler(removal, unitOfWork, logger));
}
