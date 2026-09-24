using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using Endatix.Core.Specifications.Parameters;

namespace Endatix.Core.UseCases.Submissions.ListByFormId;

public class ListByFormIdHandler(
    IRepository<Submission> submissionsRepository,
    IRepository<FormDefinition> formDefinitionsRepository
    ) : IQueryHandler<ListByFormIdQuery, Result<Paged<SubmissionDto>>>
{
    public async Task<Result<Paged<SubmissionDto>>> Handle(ListByFormIdQuery request, CancellationToken cancellationToken)
    {
        var formDefinitionsSpec = new FormDefinitionsByFormIdSpec(request.FormId);
        var formDefinitionsExist = await formDefinitionsRepository.AnyAsync(formDefinitionsSpec, cancellationToken);

        if (!formDefinitionsExist)
        {
            return Result.NotFound("Form not found.");
        }

        var pagingParams = new PagingParameters(request.Page, request.PageSize);
        var filterParams = new FilterParameters(request.FilterExpressions ?? Array.Empty<string>());
        var listFilter = new SubmissionsListFilter(
            request.SortBy,
            request.SortDescending,
            request.Created,
            request.Modified,
            request.Started,
            request.Completed);
        var totalCountSpec = new SubmissionsByFormIdCountSpec(request.FormId, filterParams, listFilter);
        var totalCount = await submissionsRepository.CountAsync(totalCountSpec, cancellationToken);
        var window = pagingParams.ForTotal(totalCount);

        IReadOnlyList<SubmissionDto> submissions = [];
        if (totalCount > 0)
        {
            var pageSpec = new SubmissionsByFormIdSpec(request.FormId, new PagingParameters(window), filterParams, listFilter);
            submissions = [.. await submissionsRepository.ListAsync(pageSpec, cancellationToken)];
        }

        return Result.Success(window.ToPaged(submissions));
    }
}
