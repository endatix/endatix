using Ardalis.Specification;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Paging;
using Endatix.Core.Specifications.Common;

namespace Endatix.Core.Specifications;

/// <summary>
/// Applies form-id and calendar date bounds without pagination (for counts).
/// </summary>
public sealed class FormDefinitionsListFilterSpec : Specification<FormDefinition>
{
    public FormDefinitionsListFilterSpec(
        long formId,
        UtcDateTimeRange created = default,
        UtcDateTimeRange modified = default)
    {
        Query
            .Where(fd => fd.FormId == formId)
            .WhereUtcRange(x => x.CreatedAt, created)
            .WhereUtcRange(x => x.ModifiedAt, modified)
            .AsNoTracking();
    }
}
