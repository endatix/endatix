using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;

namespace Endatix.Core.UseCases.CustomQuestions.List;

/// <summary>
/// Handler for retrieving all custom questions for the current tenant.
/// </summary>
public class ListCustomQuestionsHandler(IRepository<CustomQuestion> customQuestionsRepository)
    : IQueryHandler<ListCustomQuestionsQuery, Result<IEnumerable<CustomQuestion>>>
{
    /// <summary>
    /// Handles the retrieval of all custom questions for the current tenant.
    /// </summary>
    /// <param name="request">The query.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result containing the list of custom questions.</returns>
    public async Task<Result<IEnumerable<CustomQuestion>>> Handle(ListCustomQuestionsQuery request, CancellationToken cancellationToken)
    {
        var questions = await customQuestionsRepository.ListAsync(cancellationToken);
        return Result<IEnumerable<CustomQuestion>>.Success(questions);
    }
} 