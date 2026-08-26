using Ardalis.Specification;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Paging;
using Endatix.Core.Specifications.Common;
using Endatix.Core.Specifications.Parameters;
using Endatix.Core.UseCases.Forms.List;

namespace Endatix.Core.Specifications;

/// <summary>
/// Applies list filters, optional name search, and calendar date bounds without pagination (for counts).
/// </summary>
public sealed class FormsListFilterSpec : Specification<Form>
{
    public FormsListFilterSpec(
        FilterParameters filterParams,
        string? search,
        UtcDateTimeRange created = default,
        UtcDateTimeRange modified = default)
    {
        Query.Filter(filterParams).AsNoTracking();
        ApplyNameSearch(Query, search);
        Query.WhereUtcRange(x => x.CreatedAt, created);
        Query.WhereUtcRange(x => x.ModifiedAt, modified);
    }

    internal static void ApplyNameSearch(ISpecificationBuilder<Form> query, string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return;
        }

        var term = search.Trim().ToLowerInvariant();
        query.Where(form => form.Name.ToLower().Contains(term));
    }

    internal static void ApplyOrdering(
        ISpecificationBuilder<Form> query,
        FormListSortBy sortBy,
        bool sortDescending)
    {
        switch (sortBy)
        {
            case FormListSortBy.Name:
                query.OrderByWithIdTiebreaker(x => x.Name, sortDescending);
                break;
            case FormListSortBy.ModifiedAt:
                query.OrderByWithIdTiebreaker(x => x.ModifiedAt, sortDescending);
                break;
            default:
                query.OrderByWithIdTiebreaker(x => x.CreatedAt, sortDescending);
                break;
        }
    }
}
