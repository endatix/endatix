using Endatix.Core.Abstractions;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using Endatix.Core.Specifications.Parameters;

namespace Endatix.Core.UseCases.FormDefinitions.List;

/// <summary>
/// Handler for retrieving form definitions as a paged envelope.
/// Returns NotFound when the form has no matching definitions.
/// </summary>
[DirectCallOnly]
public class ListFormDefinitionsHandler(IRepository<FormDefinition> repository) : IQueryHandler<ListFormDefinitionsQuery, Result<Paged<FormDefinition>>>
{
    private ContentQueryScope Selection { get; init; } = ContentQueryScope.Repository;

    public async Task<Result<Paged<FormDefinition>>> Handle(
        ListFormDefinitionsQuery request,
        CancellationToken cancellationToken)
    {
        var countSpec = new FormDefinitionsListFilterSpec(
            request.FormId,
            request.Created,
            request.Modified);
        Selection.Apply(countSpec);
        var totalRecords = await repository.CountAsync(countSpec, cancellationToken);
        if (totalRecords == 0)
        {
            return Result.NotFound("Form not found.");
        }

        return Result.Success(await ReadPageAsync(request, totalRecords, cancellationToken));
    }

    private async Task<Paged<FormDefinition>> ReadPageAsync(ListFormDefinitionsQuery request, int totalRecords, CancellationToken cancellationToken)
    {
        var resolved = new PagingParameters(request.Page, request.PageSize).ForTotal(totalRecords);

        var spec = new FormDefinitionsListSpec(
            request.FormId,
            new PagingParameters(resolved),
            request.SortBy,
            request.SortDescending,
            request.Created,
            request.Modified);
        Selection.Apply(spec);
        IReadOnlyList<FormDefinition> items = [.. await repository.ListAsync(spec, cancellationToken)];

        return resolved.ToPaged(items);
    }

    internal sealed class Registered(IRepository<FormDefinition> repository, ContentQueryScope tenant)
        : RegisteredContentHandler<ListFormDefinitionsQuery, Result<Paged<FormDefinition>>>(
            new ListFormDefinitionsHandler(repository) { Selection = tenant });
}
