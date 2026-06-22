using Ardalis.Specification;
using Endatix.Core.Entities;
using Endatix.Core.Specifications.Common;
using Endatix.Core.Specifications.Parameters;

namespace Endatix.Core.Specifications;

/// <summary>
/// Applies list filters and optional name search without pagination (for counts).
/// </summary>
public sealed class FormsListFilterSpec : Specification<Form>
{
    public FormsListFilterSpec(FilterParameters filterParams, string? search)
    {
        Query.Filter(filterParams).AsNoTracking();
        ApplyNameSearch(Query, search);
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
}
