using Ardalis.Specification;
using Endatix.Core.Abstractions;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using Endatix.Core.Specifications.Parameters;

namespace Endatix.Core.UseCases.Submissions.ListByFormId;

public class ListByFormIdHandler(
    IRepository<Submission> submissionsRepository,
    IRepository<Form> forms, ITenantContext tenant
    ) : IQueryHandler<ListByFormIdQuery, Result<Paged<SubmissionDto>>>
{
    public async Task<Result<Paged<SubmissionDto>>> Handle(ListByFormIdQuery request, CancellationToken cancellationToken)
    {
        var id = tenant.TenantId;
        if (id <= 0)
        {
            return Result.NotFound("Form not found.");
        }
        var parent = new FormSpecifications.ByIdForTenant(request.FormId, id);
        parent.Query.Where(f => f.FormDefinitions.Any(d => d.TenantId == id));
        if (!await forms.AnyAsync(parent, cancellationToken))
        {
            return Result.NotFound("Form not found.");
        }

        return await ReadPageAsync(request, cancellationToken);
    }

    private async Task<Result<Paged<SubmissionDto>>> ReadPageAsync(ListByFormIdQuery request, CancellationToken cancellationToken)
    {
        var pagingParams = new PagingParameters(request.Page, request.PageSize);
        var filterParams = new FilterParameters(request.FilterExpressions ?? Array.Empty<string>());
        var listFilter = new SubmissionsListFilter(request.SortBy, request.SortDescending, request.Created,
            request.Modified, request.Started, request.Completed);
        var totalCountSpec = new SubmissionsByFormIdCountSpec(request.FormId, filterParams, listFilter);
        totalCountSpec.OnLiveForm(tenant);
        var totalCount = await submissionsRepository.CountAsync(totalCountSpec, cancellationToken);
        var resolved = pagingParams.ForTotal(totalCount);

        IReadOnlyList<SubmissionDto> submissions = [];
        if (totalCount > 0)
        {
            var pageSpec = new SubmissionsByFormIdSpec(request.FormId, new PagingParameters(resolved), filterParams, listFilter);
            pageSpec.OnLiveForm(tenant);
            submissions = [.. await submissionsRepository.ListAsync(pageSpec, cancellationToken)];
        }

        return Result.Success(resolved.ToPaged(submissions));
    }
}
