using Ardalis.Specification;
using Endatix.Core.Abstractions;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using Endatix.Core.Specifications.Parameters;

namespace Endatix.Core.UseCases.Submissions.ListByFormId;

/// <summary>Lists submissions for a form.</summary>
[DirectCallOnly]
public class ListByFormIdHandler : IQueryHandler<ListByFormIdQuery, Result<Paged<SubmissionDto>>>
{
    private readonly IRepository<Submission> _submissionsRepository;
    private readonly ISubmissionListParent _parent;
    private readonly ContentQueryScope _content;

    /// <summary>Uses the supplied repositories for direct calls.</summary>
    public ListByFormIdHandler(IRepository<Submission> submissionsRepository,
        IRepository<FormDefinition> formDefinitionsRepository)
        : this(submissionsRepository, new RepositoryDefinitionParent(formDefinitionsRepository), ContentQueryScope.Repository) { }

    private ListByFormIdHandler(IRepository<Submission> submissionsRepository, ISubmissionListParent parent, ContentQueryScope content)
    {
        _submissionsRepository = submissionsRepository;
        _parent = parent;
        _content = content;
    }

    public async Task<Result<Paged<SubmissionDto>>> Handle(ListByFormIdQuery request, CancellationToken cancellationToken)
    {
        if (!await _parent.HasDefinitionsAsync(request.FormId, cancellationToken))
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
        var totalCountSpec = _content.Submission(new SubmissionsByFormIdCountSpec(request.FormId, filterParams, listFilter));
        var totalCount = await _submissionsRepository.CountAsync(totalCountSpec, cancellationToken);
        var resolved = pagingParams.ForTotal(totalCount);

        IReadOnlyList<SubmissionDto> submissions = [];
        if (totalCount > 0)
        {
            var pageSpec = _content.Submission(new SubmissionsByFormIdSpec(request.FormId, new PagingParameters(resolved), filterParams, listFilter));
            submissions = [.. await _submissionsRepository.ListAsync(pageSpec, cancellationToken)];
        }

        return Result.Success(resolved.ToPaged(submissions));
    }

    internal sealed class Registered(IRepository<Submission> submissionsRepository, ISubmissionListParent parent, ContentQueryScope content)
        : RegisteredContentHandler<ListByFormIdQuery, Result<Paged<SubmissionDto>>>(
            new ListByFormIdHandler(submissionsRepository, parent, content));
}
