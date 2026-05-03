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
        var filterParams = new FilterParameters(request.FilterExpressions!);
        var formByIdSpec = new SubmissionsByFormIdSpec(request.FormId, pagingParams, filterParams);
        var totalCountSpec = new SubmissionsByFormIdCountSpec(request.FormId, filterParams);

        var totalCount = await submissionsRepository.CountAsync(totalCountSpec, cancellationToken);
        var submissions = totalCount <= 0
            ? []
            : await submissionsRepository.ListAsync(formByIdSpec, cancellationToken);

        var skip = (pagingParams.Page - 1) * pagingParams.PageSize;
        var paged = Paged<SubmissionDto>.FromSkipAndTake(
            skip: skip,
            take: pagingParams.PageSize,
            totalRecords: totalCount,
            items: [.. submissions]);

        return Result.Success(paged);
    }
}
