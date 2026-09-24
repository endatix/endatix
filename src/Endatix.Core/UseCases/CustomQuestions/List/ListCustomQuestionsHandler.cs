using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using Endatix.Core.Specifications.Parameters;

namespace Endatix.Core.UseCases.CustomQuestions.List;

/// <summary>
/// Handler for retrieving custom questions for the current tenant.
/// </summary>
public class ListCustomQuestionsHandler(IRepository<CustomQuestion> customQuestionsRepository)
    : IQueryHandler<ListCustomQuestionsQuery, Result<Paged<CustomQuestion>>>
{
    /// <inheritdoc />
    public async Task<Result<Paged<CustomQuestion>>> Handle(
        ListCustomQuestionsQuery request,
        CancellationToken cancellationToken)
    {
        var pagingParams = new PagingParameters(request.Page, request.PageSize);

        var countSpec = new CustomQuestionSpecifications.ListFilter(
            request.Created,
            request.Modified);
        var totalRecords = await customQuestionsRepository.CountAsync(countSpec, cancellationToken);

        var resolved = pagingParams.ForTotal(totalRecords);
        var queryPagingParams = new PagingParameters(resolved);

        IReadOnlyList<CustomQuestion> items = [];
        if (totalRecords > 0)
        {
            var pageSpec = new CustomQuestionSpecifications.ListSpec(
                queryPagingParams,
                request.SortBy,
                request.SortDescending,
                request.Created,
                request.Modified);
            items = await customQuestionsRepository.ListAsync(pageSpec, cancellationToken);
        }

        return Result.Success(resolved.ToPaged(items));
    }
}
