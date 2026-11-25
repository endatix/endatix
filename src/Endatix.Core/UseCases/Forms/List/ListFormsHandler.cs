using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using Endatix.Core.Specifications.Parameters;
using Microsoft.Extensions.Logging;

namespace Endatix.Core.UseCases.Forms.List;

/// <summary>
/// Handler for listing forms.
/// </summary>
[DirectCallOnly]
public sealed class ListFormsHandler(IFormsRepository repository,
    ILogger<ListFormsHandler> logger) : IQueryHandler<ListFormsQuery, Result<Paged<FormDto>>>
{
    private ContentQueryScope Selection { get; init; } = ContentQueryScope.Repository;

    /// <inheritdoc/>
    public async Task<Result<Paged<FormDto>>> Handle(
        ListFormsQuery request,
        CancellationToken cancellationToken)
    {
        if (Selection.TenantId <= 0)
        {
            return Result.Unauthorized("Tenant context is required.");
        }

        try
        {
            var filter = CreateFilterParameters(request);
            var count = new FormsListFilterSpec(filter, request.Search?.Trim(), request.Created, request.Modified);
            Selection.Apply(count);
            var total = await repository.CountAsync(count, cancellationToken);
            var resolved = new PagingParameters(request.Page, request.PageSize).ForTotal(total);
            var forms = total == 0 ? [] : await LoadFormsPageAsync(new PagingParameters(resolved), (request, filter), cancellationToken);
            return Result.Success(resolved.ToPaged([.. forms]));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to list forms");
            return Result.Error("Failed to list forms.");
        }
    }

    private async Task<IEnumerable<FormDto>> LoadFormsPageAsync(
        PagingParameters pagingParams, (ListFormsQuery Request, FilterParameters Filter) page, CancellationToken cancellationToken)
    {
        var (request, filterParams) = page;
        var searchFormSpec = new FormsWithSubmissionsCountSpec(
            pagingParams,
            filterParams,
            request.Search?.Trim(),
            request.SortBy,
            request.SortDescending,
            request.Created,
            request.Modified);

        Selection.Apply(searchFormSpec);
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

    internal sealed class Registered(IFormsRepository repository, ILoggerFactory loggers, ContentQueryScope tenant)
        : RegisteredContentHandler<ListFormsQuery, Result<Paged<FormDto>>>(
            new ListFormsHandler(repository, loggers.CreateLogger<ListFormsHandler>()) { Selection = tenant });
}
