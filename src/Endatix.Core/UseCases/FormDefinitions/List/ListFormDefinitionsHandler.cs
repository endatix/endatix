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
public class ListFormDefinitionsHandler(IRepository<FormDefinition> repository)
    : IQueryHandler<ListFormDefinitionsQuery, Result<Paged<FormDefinition>>>
{
    public async Task<Result<Paged<FormDefinition>>> Handle(
        ListFormDefinitionsQuery request,
        CancellationToken cancellationToken)
    {
        var pagingParams = new PagingParameters(request.Page, request.PageSize);
        var countSpec = new FormDefinitionsListFilterSpec(
            request.FormId,
            request.Created,
            request.Modified);
        var totalRecords = await repository.CountAsync(countSpec, cancellationToken);
        if (totalRecords == 0)
        {
            return Result.NotFound("Form not found.");
        }

        var page = Paged<FormDefinition>.ResolvePage(
            pagingParams.Page,
            pagingParams.PageSize,
            totalRecords);

        var spec = new FormDefinitionsListSpec(
            request.FormId,
            new PagingParameters(page, pagingParams.PageSize),
            request.SortBy,
            request.SortDescending,
            request.Created,
            request.Modified);
        IReadOnlyList<FormDefinition> items = [.. await repository.ListAsync(spec, cancellationToken)];

        return Result.Success(Paged<FormDefinition>.FromPage(
            page,
            pagingParams.PageSize,
            totalRecords,
            items));
    }
}
