using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.Submissions;

/// <summary>
/// Query for getting a submission by FormId and SubmissionId
/// </summary>
/// <param name="FormId"></param>
/// <param name="SubmissionId"></param>
public record GetByIdQuery(long FormId, long SubmissionId) : IQuery<Result<Submission>> { }
