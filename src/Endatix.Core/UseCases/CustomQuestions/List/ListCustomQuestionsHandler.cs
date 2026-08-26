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
            request.CreatedFrom,
            request.CreatedTo,
            request.ModifiedFrom,
            request.ModifiedTo);
        var totalRecords = await customQuestionsRepository.CountAsync(countSpec, cancellationToken);

        var page = Paged<CustomQuestion>.ResolvePage(
            pagingParams.Page,
            pagingParams.PageSize,
            totalRecords);
        var queryPagingParams = new PagingParameters(page, pagingParams.PageSize);

        IReadOnlyList<CustomQuestion> items = [];
        if (totalRecords > 0)
        {
            var pageSpec = new CustomQuestionSpecifications.ListSpec(
                queryPagingParams,
                request.SortBy,
                request.SortDescending,
                request.CreatedFrom,
                request.CreatedTo,
                request.ModifiedFrom,
                request.ModifiedTo);
            items = await customQuestionsRepository.ListAsync(pageSpec, cancellationToken);
        }

        var paged = Paged<CustomQuestion>.FromPage(
            page,
            pagingParams.PageSize,
            totalRecords,
            items);

        return Result.Success(paged);
    }
}
