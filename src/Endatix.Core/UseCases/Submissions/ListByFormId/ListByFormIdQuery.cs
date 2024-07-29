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
public record ListByFormIdQuery(long FormId, int? Page, int? PageSize) : IQuery<Result<IEnumerable<Submission>>>
{ }
