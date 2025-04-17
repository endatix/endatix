using Ardalis.GuardClauses;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;

namespace Endatix.Core.UseCases.Themes.GetFormsByThemeId;

/// <summary>
/// Handler for retrieving all forms using a specific theme.
/// </summary>
public class GetFormsByThemeIdHandler(IRepository<Form> formsRepository) : IQueryHandler<GetFormsByThemeIdQuery, Result<List<Form>>>
{
    /// <summary>
    /// Handles the retrieval of forms using a specific theme.
    /// </summary>
    /// <param name="request">The query containing the theme ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result containing a list of forms.</returns>
    public async Task<Result<List<Form>>> Handle(GetFormsByThemeIdQuery request, CancellationToken cancellationToken)
    {
        Guard.Against.NegativeOrZero(request.ThemeId, nameof(request.ThemeId));

        try
        {
            var formsUsingThemeSpecification = new FormSpecifications.ByThemeId(request.ThemeId);
            var forms = await formsRepository.ListAsync(formsUsingThemeSpecification, cancellationToken);

            return Result<List<Form>>.Success(forms.ToList());
        }
        catch (Exception ex)
        {
            return Result<List<Form>>.Error($"Error retrieving forms: {ex.Message}");
        }
    }
}