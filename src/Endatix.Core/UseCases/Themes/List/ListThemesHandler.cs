using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Paging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using Endatix.Core.Specifications.Parameters;

namespace Endatix.Core.UseCases.Themes.List;

/// <summary>
/// Handler for retrieving themes as a paged envelope.
/// </summary>
public class ListThemesHandler(IRepository<Theme> themeRepository, ManagedContentScope? scope)
    : IQueryHandler<ListThemesQuery, Result<Paged<Theme>>>
{
    /// <summary>Constructs the ordinary operation without account standing enforcement.</summary>
    public ListThemesHandler(IRepository<Theme> themeRepository)
        : this(themeRepository, null) { }

    /// <inheritdoc />
    public async Task<Result<Paged<Theme>>> Handle(
        ListThemesQuery request,
        CancellationToken cancellationToken)
    {
        if (scope is not null && await scope.RefusalAsync<Paged<Theme>>(cancellationToken) is { } refusal)
        {
            return refusal;
        }

        var count = new ThemeSpecifications.ListFilter(request.Created, request.Modified);
        count.InTenant(scope);
        var total = await themeRepository.CountAsync(count, cancellationToken);
        var page = new PagingParameters(request.Page, request.PageSize).ForTotal(total);
        var items = total == 0 ? [] : await ReadPageAsync(request, page, cancellationToken);
        return Result.Success(page.ToPaged(items));
    }

    private async Task<IReadOnlyList<Theme>> ReadPageAsync(ListThemesQuery request, ResolvedPage page, CancellationToken ct)
    {
        var spec = new ThemeSpecifications.Paginated(new PagingParameters(page), request.SortBy,
            request.SortDescending, request.Created, request.Modified);
        spec.InTenant(scope);
        return await themeRepository.ListAsync(spec, ct);
    }
}
