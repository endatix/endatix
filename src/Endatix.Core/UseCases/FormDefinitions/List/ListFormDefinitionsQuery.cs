using Ardalis.GuardClauses;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.FormDefinitions.List;

/// <summary>
/// Query for listing form definitions with pagination, sort, and date bounds.
/// </summary>
public sealed record ListFormDefinitionsQuery : IQuery<Result<IEnumerable<FormDefinition>>>
{
    public long FormId { get; init; }
    public int? Page { get; init; }
    public int? PageSize { get; init; }
    public FormDefinitionListSortBy SortBy { get; init; }
    public bool SortDescending { get; init; }
    public DateTime? CreatedFrom { get; init; }
    public DateTime? CreatedTo { get; init; }
    public DateTime? ModifiedFrom { get; init; }
    public DateTime? ModifiedTo { get; init; }

    public ListFormDefinitionsQuery(
        long formId,
        int? page = null,
        int? pageSize = null,
        FormDefinitionListSortBy sortBy = FormDefinitionListSortBy.CreatedAt,
        bool sortDescending = true,
        DateTime? createdFrom = null,
        DateTime? createdTo = null,
        DateTime? modifiedFrom = null,
        DateTime? modifiedTo = null)
    {
        Guard.Against.NegativeOrZero(formId);

        FormId = formId;
        Page = page;
        PageSize = pageSize;
        SortBy = sortBy;
        SortDescending = sortDescending;
        CreatedFrom = createdFrom;
        CreatedTo = createdTo;
        ModifiedFrom = modifiedFrom;
        ModifiedTo = modifiedTo;
    }
}
