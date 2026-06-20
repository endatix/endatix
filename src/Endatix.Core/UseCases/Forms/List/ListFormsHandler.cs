using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using Endatix.Core.Specifications.Parameters;

namespace Endatix.Core.UseCases.Forms.List;

/// <summary>
/// Handler for listing forms.
/// </summary>
public sealed class ListFormsHandler(IFormsRepository repository)
    : IQueryHandler<ListFormsQuery, Result<Paged<FormDto>>>
{
    /// <inheritdoc/>
    public async Task<Result<Paged<FormDto>>> Handle(
        ListFormsQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            var pagingParams = new PagingParameters(request.Page, request.PageSize);
            var filterParams = CreateFilterParameters(request);
            var search = request.Search?.Trim();

            var countSpec = new FormsListFilterSpec(filterParams, search);
            var totalRecords = await repository.CountAsync(countSpec, cancellationToken);

            var page = Paged<FormDto>.ResolvePage(
                pagingParams.Page,
                pagingParams.PageSize,
                totalRecords);
            var queryPagingParams = new PagingParameters(page, pagingParams.PageSize);

            var forms = await LoadFormsPageAsync(
                totalRecords,
                queryPagingParams,
                filterParams,
                search,
                cancellationToken);

            var paged = Paged<FormDto>.FromPage(
                page: page,
                pageSize: pagingParams.PageSize,
                totalRecords: totalRecords,
                items: [.. forms]);

            return Result.Success(paged);
        }
        catch (Exception ex)
        {
            return Result.Error($"Error listing forms: {ex.Message}");
        }
    }

    private async Task<IEnumerable<FormDto>> LoadFormsPageAsync(
        int totalRecords,
        PagingParameters pagingParams,
        FilterParameters filterParams,
        string? search,
        CancellationToken cancellationToken)
    {
        if (totalRecords <= 0)
        {
            return [];
        }

        var searchFormSpec = new FormsWithSubmissionsCountSpec(pagingParams, filterParams, search);

        return await repository.ListAsync(searchFormSpec, cancellationToken);
    }

    private static FilterParameters CreateFilterParameters(ListFormsQuery request)
    {
        var filterList = new List<string>();
        if (request.FilterExpressions is not null)
        {
            filterList.AddRange(request.FilterExpressions);
        }

        if (request.FolderId.HasValue)
        {
            filterList.Add($"FolderId:{request.FolderId.Value}");
        }

        if (request.IsEnabled.HasValue)
        {
            filterList.Add($"isEnabled:{request.IsEnabled.Value.ToString().ToLowerInvariant()}");
        }

        if (request.IsPublic.HasValue)
        {
            filterList.Add($"isPublic:{request.IsPublic.Value.ToString().ToLowerInvariant()}");
        }

        return new FilterParameters(filterList);
    }
}
