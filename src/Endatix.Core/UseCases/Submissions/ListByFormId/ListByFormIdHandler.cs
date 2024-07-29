using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Endatix.Core.Entities;
using Endatix.Core.Filters;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;

namespace Endatix.Core.UseCases.Submissions.ListByFormId;

public class ListByFormIdHandler : IQueryHandler<ListByFormIdQuery, Result<IEnumerable<Submission>>>
{
    private readonly IRepository<Submission> _submissionsRepository;
    private IRepository<FormDefinition> _formDefinitionsRepository;

    public ListByFormIdHandler(IRepository<Submission> submissionsRepository, IRepository<FormDefinition> formDefinitionsRepository)
    {
        _submissionsRepository = submissionsRepository;
        _formDefinitionsRepository = formDefinitionsRepository;
    }

    public async Task<Result<IEnumerable<Submission>>> Handle(ListByFormIdQuery request, CancellationToken cancellationToken)
    {
        var formDefinitionsSpec = new FormDefinitionsByFormIdSpec(request.FormId);
        var formDefinitionsExist = await _formDefinitionsRepository.AnyAsync(formDefinitionsSpec, cancellationToken);

        if (!formDefinitionsExist)
        {
            return Result.NotFound("Form not found.");
        }

        var pageFilter = new PagingFilter(request.Page, request.PageSize);
        var formByIdSpec = new SubmissionsByFormIdSpec(request.FormId, pageFilter);

        IEnumerable<Submission> submissions = await _submissionsRepository
                .ListAsync(formByIdSpec, cancellationToken);

        return Result.Success(submissions);
    }
}
