using Endatix.Core.Abstractions;
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
[DirectCallOnly]
public class ListCustomQuestionsHandler(IRepository<CustomQuestion> customQuestionsRepository) : IQueryHandler<ListCustomQuestionsQuery, Result<Paged<CustomQuestion>>>
{
    private ContentQueryScope Selection { get; init; } = ContentQueryScope.Repository;

    /// <inheritdoc />
    public async Task<Result<Paged<CustomQuestion>>> Handle(
        ListCustomQuestionsQuery request,
        CancellationToken cancellationToken)
    {
        if (Selection.TenantId <= 0)
        {
            return Result.Unauthorized("Tenant context is required.");
        }

        var pagingParams = new PagingParameters(request.Page, request.PageSize);

        var countSpec = new CustomQuestionSpecifications.ListFilter(
            request.Created,
            request.Modified);
        Selection.Apply(countSpec);
        var totalRecords = await customQuestionsRepository.CountAsync(countSpec, cancellationToken);

        var resolved = pagingParams.ForTotal(totalRecords);
        var items = totalRecords == 0 ? [] : await ReadPageAsync(request, new PagingParameters(resolved), cancellationToken);
        return Result.Success(resolved.ToPaged(items));
    }

    private async Task<IReadOnlyList<CustomQuestion>> ReadPageAsync(ListCustomQuestionsQuery request, PagingParameters page, CancellationToken ct)
    {
        var spec = new CustomQuestionSpecifications.ListSpec(page, request.SortBy, request.SortDescending, request.Created, request.Modified);
        Selection.Apply(spec);
        return await customQuestionsRepository.ListAsync(spec, ct);
    }

    internal sealed class Registered(IRepository<CustomQuestion> customQuestionsRepository, ContentQueryScope tenant)
        : RegisteredContentHandler<ListCustomQuestionsQuery, Result<Paged<CustomQuestion>>>(
            new ListCustomQuestionsHandler(customQuestionsRepository) { Selection = tenant });
}
