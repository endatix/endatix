using Endatix.Core.Abstractions;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Paging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using Endatix.Core.Specifications.Parameters;

namespace Endatix.Core.UseCases.FormTemplates.List;

/// <summary>Returns a page of templates for the current tenant.</summary>
[DirectCallOnly]
public class ListFormTemplatesHandler(IRepository<FormTemplate> repository) : IQueryHandler<ListFormTemplatesQuery, Result<Paged<FormTemplateDto>>>
{
    private ContentQueryScope Selection { get; init; } = ContentQueryScope.Repository;

    public async Task<Result<Paged<FormTemplateDto>>> Handle(
        ListFormTemplatesQuery request,
        CancellationToken cancellationToken)
    {

        if (Selection is { TenantId: <= 0 })
        {
            return Result.Unauthorized("Tenant context is required.");
        }

        var filters = CreateFilterParameters(request.FilterExpressions, request.FolderId);
        var count = new FormTemplatesListFilterSpec(filters, request.Created, request.Modified);
        Selection.Apply(count);
        var total = await repository.CountAsync(count, cancellationToken);
        var page = new PagingParameters(request.Page, request.PageSize).ForTotal(total);
        var items = total == 0 ? [] : await ReadPageAsync(request, (page, filters), cancellationToken);
        return Result.Success(page.ToPaged(items));
    }

    private async Task<IReadOnlyList<FormTemplateDto>> ReadPageAsync(ListFormTemplatesQuery request, (ResolvedPage Window, FilterParameters Filters) page, CancellationToken ct)
    {
        var spec = new FormTemplatesSpec(new PagingParameters(page.Window), page.Filters, request.SortBy,
            request.SortDescending, request.Created, request.Modified);
        Selection.Apply(spec);
        return await repository.ListAsync(spec, ct);
    }

    private static FilterParameters CreateFilterParameters(
        IEnumerable<string>? filterExpressions,
        long? folderId)
    {
        var filterList = new List<string>();
        if (filterExpressions is not null)
        {
            filterList.AddRange(filterExpressions);
        }

        if (folderId.HasValue)
        {
            filterList.Add($"FolderId:{folderId.Value}");
        }

        return new FilterParameters(filterList);
    }

    internal sealed class Registered(IRepository<FormTemplate> repository, ContentQueryScope scope)
        : RegisteredContentHandler<ListFormTemplatesQuery, Result<Paged<FormTemplateDto>>>(
            new ListFormTemplatesHandler(repository) { Selection = scope });
}
