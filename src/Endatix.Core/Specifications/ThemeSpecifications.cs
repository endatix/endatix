using Ardalis.Specification;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Paging;
using Endatix.Core.Specifications.Parameters;
using Endatix.Core.Specifications.Common;
using Endatix.Core.UseCases.Themes.List;

namespace Endatix.Core.Specifications;

/// <summary>
/// Specifications for working with Theme entities
/// </summary>
public static class ThemeSpecifications
{
    /// <summary>
    /// Specification to get a theme by ID with forms
    /// </summary>
    public sealed class ByIdWithForms : Specification<Theme>, ISingleResultSpecification<Theme>
    {
        public ByIdWithForms(long id)
        {
            Query
                .Where(t => t.Id == id)
                .Include(t => t.Forms);
        }
    }

    /// <summary>
    /// Specification to get a theme by name (case-insensitive)
    /// </summary>
    public sealed class ByName : Specification<Theme>, ISingleResultSpecification<Theme>
    {
        public ByName(string name)
        {
            Query.Where(t => t.Name.ToLower() == name.ToLower());
        }
    }

    /// <summary>
    /// Specification to get themes with name containing filter text
    /// </summary>
    public sealed class WithNameContaining : Specification<Theme>
    {
        public WithNameContaining(string filterText)
        {
            Query.Where(t => t.Name.ToLower().Contains(filterText.ToLower()));
        }
    }

    /// <summary>
    /// Specification to get themes with pagination, sort, and calendar date bounds.
    /// </summary>
    public sealed class Paginated : Specification<Theme>
    {
        public Paginated(
            PagingParameters pagingParams,
            ThemeListSortBy sortBy = ThemeListSortBy.ModifiedAt,
            bool sortDescending = true,
            UtcDateTimeRange created = default,
            UtcDateTimeRange modified = default)
        {
            Query
                .WhereUtcRange(x => x.CreatedAt, created)
                .WhereUtcRange(x => x.ModifiedAt, modified);

            ApplyOrdering(Query, sortBy, sortDescending);

            Query
                .Paginate(pagingParams)
                .AsNoTracking();
        }

        private static void ApplyOrdering(
            ISpecificationBuilder<Theme> query,
            ThemeListSortBy sortBy,
            bool sortDescending)
        {
            switch (sortBy)
            {
                case ThemeListSortBy.Name:
                    query.OrderByWithIdTiebreaker(x => x.Name, sortDescending);
                    break;
                case ThemeListSortBy.CreatedAt:
                    query.OrderByWithIdTiebreaker(x => x.CreatedAt, sortDescending);
                    break;
                default:
                    query.OrderByWithIdTiebreaker(x => x.ModifiedAt, sortDescending);
                    break;
            }
        }
    }
}
