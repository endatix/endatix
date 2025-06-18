using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.Submissions.GetFiles;
 
public record GetFilesQuery(long FormId, long SubmissionId, string? FileNamesPrefix) : IQuery<Result<GetFilesResult>>; 