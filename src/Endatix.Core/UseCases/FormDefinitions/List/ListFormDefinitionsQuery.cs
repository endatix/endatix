using Ardalis.GuardClauses;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Paging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.FormDefinitions.List;

/// <summary>
/// Query for listing form definitions as a paged envelope.
/// </summary>
public sealed record ListFormDefinitionsQuery : IQuery<Result<Paged<FormDefinition>>>
{
    public long FormId { get; init; }
    public int? Page { get; init; }
    public int? PageSize { get; init; }
    public FormDefinitionListSortBy SortBy { get; init; }
    public bool SortDescending { get; init; }
    public UtcDateTimeRange Created { get; init; }
    public UtcDateTimeRange Modified { get; init; }

    public ListFormDefinitionsQuery(
        long formId,
        int? page = null,
        int? pageSize = null,
        FormDefinitionListSortBy sortBy = FormDefinitionListSortBy.CreatedAt,
        bool sortDescending = true,
        UtcDateTimeRange created = default,
        UtcDateTimeRange modified = default)
    {
        Guard.Against.NegativeOrZero(formId);

        FormId = formId;
        Page = page;
        PageSize = pageSize;
        SortBy = sortBy;
        SortDescending = sortDescending;
        Created = created;
        Modified = modified;
    }
}
