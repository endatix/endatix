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
public class ListCustomQuestionsHandler(IRepository<CustomQuestion> customQuestionsRepository, ITenantContext tenant)
    : IQueryHandler<ListCustomQuestionsQuery, Result<Paged<CustomQuestion>>>
{
    /// <inheritdoc />
    public async Task<Result<Paged<CustomQuestion>>> Handle(
        ListCustomQuestionsQuery request,
        CancellationToken cancellationToken)
    {
        if (tenant.TenantId <= 0)
        {
            return Result.Unauthorized("Tenant context is required.");
        }

        var pagingParams = new PagingParameters(request.Page, request.PageSize);

        var countSpec = new CustomQuestionSpecifications.ListFilter(
            request.Created,
            request.Modified);
        countSpec.InTenant(tenant);
        var totalRecords = await customQuestionsRepository.CountAsync(countSpec, cancellationToken);

        var resolved = pagingParams.ForTotal(totalRecords);
        var items = totalRecords == 0 ? [] : await ReadPageAsync(request, new PagingParameters(resolved), cancellationToken);
        return Result.Success(resolved.ToPaged(items));
    }

    private async Task<IReadOnlyList<CustomQuestion>> ReadPageAsync(ListCustomQuestionsQuery request, PagingParameters page, CancellationToken ct)
    {
        var spec = new CustomQuestionSpecifications.ListSpec(page, request.SortBy, request.SortDescending, request.Created, request.Modified);
        spec.InTenant(tenant);
        return await customQuestionsRepository.ListAsync(spec, ct);
    }
}
