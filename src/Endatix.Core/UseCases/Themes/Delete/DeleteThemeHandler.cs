using Ardalis.GuardClauses;
using Endatix.Core.Abstractions.Data;
using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;

namespace Endatix.Core.UseCases.Themes.Delete;

/// <summary>
/// Handler for deleting a theme.
/// </summary>
public class DeleteThemeHandler(
    IRepository<Theme> themeRepository,
    IRepository<Form> formRepository,
    IUnitOfWork unitOfWork
) : ICommandHandler<DeleteThemeCommand, Result<string>>
{
    /// <summary>
    /// Handles the deletion of a theme with transaction support.
    /// </summary>
    /// <param name="request">The command containing the theme ID to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Success result if deleted, NotFound if theme doesn't exist.</returns>
    public async Task<Result<string>> Handle(DeleteThemeCommand request, CancellationToken cancellationToken)
    {
        Guard.Against.NegativeOrZero(request.ThemeId, nameof(request.ThemeId));

        try
        {
            await unitOfWork.BeginTransactionAsync(cancellationToken);

            var theme = await themeRepository.GetByIdAsync(request.ThemeId, cancellationToken);
            if (theme == null)
            {
                await unitOfWork.RollbackTransactionAsync(cancellationToken);
                return Result.NotFound($"Theme with ID {request.ThemeId} not found");
            }

            // Check if there are forms using this theme
            var forms = await formRepository.ListAsync(
                new FormSpecifications.ByThemeId(request.ThemeId),
                cancellationToken);

            if (forms.Any())
            {
                // Remove theme from all forms
                foreach (var form in forms)
                {
                    form.SetTheme(null);
                }
            }

            await themeRepository.DeleteAsync(theme, cancellationToken);
            
            // Single SaveChanges for all modifications
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await unitOfWork.CommitTransactionAsync(cancellationToken);

            return Result.Success(theme.Id.ToString());
        }
        catch (Exception ex)
        {
            await unitOfWork.RollbackTransactionAsync(cancellationToken);
            return Result.Error($"Error deleting theme: {ex.Message}");
        }
    }
}