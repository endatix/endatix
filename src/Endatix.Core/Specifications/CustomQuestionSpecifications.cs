using Ardalis.Specification;
using Endatix.Core.Entities;
using Endatix.Core.Specifications.Common;
using Endatix.Core.Specifications.Parameters;
using Endatix.Core.UseCases.CustomQuestions.List;

namespace Endatix.Core.Specifications;

/// <summary>
/// Specifications for working with CustomQuestion entities
/// </summary>
public static class CustomQuestionSpecifications
{
    /// <summary>
    /// Specification to get a custom question by name (case-insensitive)
    /// </summary>
    public sealed class ByName : Specification<CustomQuestion>, ISingleResultSpecification<CustomQuestion>
    {
        public ByName(string name)
        {
            Query.Where(q => q.Name.ToLower() == name.ToLower());
        }
    }

    public class ByTenantId : Specification<CustomQuestion>
    {
        public ByTenantId(long tenantId)
        {
            Query.Where(q => q.TenantId == tenantId);
        }
    }

    /// <summary>
    /// Filter-only spec for counting with created/modified bounds.
    /// </summary>
    public sealed class ListFilter : Specification<CustomQuestion>
    {
        public ListFilter(
            DateTime? createdFrom = null,
            DateTime? createdTo = null,
            DateTime? modifiedFrom = null,
            DateTime? modifiedTo = null)
        {
            Query
                .WhereCreatedRange(createdFrom, createdTo)
                .WhereModifiedRange(modifiedFrom, modifiedTo)
                .AsNoTracking();
        }
    }

    /// <summary>
    /// Paged list with sort and created/modified date bounds.
    /// </summary>
    public sealed class ListSpec : Specification<CustomQuestion>
    {
        public ListSpec(
            PagingParameters pagingParams,
            CustomQuestionListSortBy sortBy = CustomQuestionListSortBy.CreatedAt,
            bool sortDescending = true,
            DateTime? createdFrom = null,
            DateTime? createdTo = null,
            DateTime? modifiedFrom = null,
            DateTime? modifiedTo = null)
        {
            Query
                .WhereCreatedRange(createdFrom, createdTo)
                .WhereModifiedRange(modifiedFrom, modifiedTo);

            switch (sortBy)
            {
                case CustomQuestionListSortBy.ModifiedAt:
                    Query.OrderByWithIdTiebreaker(x => x.ModifiedAt, sortDescending);
                    break;
                default:
                    Query.OrderByWithIdTiebreaker(x => x.CreatedAt, sortDescending);
                    break;
            }

            Query.Paginate(pagingParams).AsNoTracking();
        }
    }
}
