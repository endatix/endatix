using System.Collections.Generic;
using Ardalis.GuardClauses;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.FormDefinitions.List;

/// <summary>
/// Query for listing form definitions with pagination.
/// </summary>
public record ListFormDefinitionsQuery(long FormId, int? Page, int? PageSize) : IQuery<Result<IEnumerable<FormDefinition>>>;
