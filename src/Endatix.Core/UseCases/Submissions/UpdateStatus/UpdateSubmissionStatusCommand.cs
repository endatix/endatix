using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.Submissions.UpdateStatus;

public record UpdateSubmissionStatusCommand(
    long SubmissionId,
    long FormId,
    string StatusCode) : ICommand<Result<UpdateSubmissionStatusResponse>>;

public record UpdateSubmissionStatusResponse(
    long Id,
    string Status,
    DateTime? UpdatedAt); 