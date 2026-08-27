using Ardalis.Specification;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Paging;
using Endatix.Core.Specifications.Common;
using Endatix.Core.Specifications.Parameters;

namespace Endatix.Core.Specifications;

/// <summary>
/// Applies list filters and calendar date bounds without pagination (for counts).
/// </summary>
public sealed class FormTemplatesListFilterSpec : Specification<FormTemplate>
{
    public FormTemplatesListFilterSpec(
        FilterParameters filterParams,
        UtcDateTimeRange created = default,
        UtcDateTimeRange modified = default)
    {
        Query
            .Filter(filterParams)
            .WhereUtcRange(x => x.CreatedAt, created)
            .WhereUtcRange(x => x.ModifiedAt, modified)
            .AsNoTracking();
    }
}
