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
    ) : IQueryHandler<ListByFormIdQuery, Result<IEnumerable<Submission>>>
{
    public async Task<Result<IEnumerable<Submission>>> Handle(ListByFormIdQuery request, CancellationToken cancellationToken)
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

        IEnumerable<Submission> submissions = await submissionsRepository
                .ListAsync(formByIdSpec, cancellationToken);

        return Result.Success(submissions);
    }
}
