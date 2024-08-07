using System.Collections.Generic;
using Ardalis.GuardClauses;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.FormDefinitions.List;

/// <summary>
/// Query for listing form definitions with pagination.
/// </summary>
public record ListFormDefinitionsQuery : IQuery<Result<IEnumerable<FormDefinition>>>
{
    public long FormId { get; init; }
    public int? Page { get; init; }
    public int? PageSize { get; init; }

    public ListFormDefinitionsQuery(long formId, int? page, int? pageSize)
    {
        Guard.Against.NegativeOrZero(formId);

        FormId = formId;
        Page = page;
        PageSize = pageSize;
    }
}
