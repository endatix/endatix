using System.Collections.Generic;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.Submissions.ListByFormId;

/// <summary>
/// Query for listing forms with pagination.
/// </summary>
/// <param name="FormId"></param>
/// <param name="Page"></param>
/// <param name="PageSize"></param>
/// <param name="FilterExpressions"></param>
public record ListByFormIdQuery(long FormId, int? Page, int? PageSize, IEnumerable<string>? FilterExpressions = null) : IQuery<Result<IEnumerable<Submission>>>
{ }
