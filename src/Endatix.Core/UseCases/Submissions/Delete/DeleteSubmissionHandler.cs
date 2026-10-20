using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;

namespace Endatix.Core.UseCases.Submissions.Delete;

public class DeleteSubmissionHandler(IRepository<Submission> repository) : ICommandHandler<DeleteSubmissionCommand, Result<Submission>>
{
    public async Task<Result<Submission>> Handle(DeleteSubmissionCommand request, CancellationToken cancellationToken)
    {
        // A submission of a deleted form is not found, as for every other write.
        var spec = new SubmissionByFormIdAndSubmissionIdSpec(request.FormId, request.SubmissionId);
        var submission = await repository.SingleOrDefaultAsync(spec, cancellationToken);
        if (submission is null)
        {
            return Result.NotFound("Submission not found");
        }

        submission.Delete();

        // Deleted meanwhile, on its own or with its form: the outcome the caller asked for, but not this delete's.
        var saved = await repository.TrySaveChangesAsync(cancellationToken);
        return saved.IsSuccess ? Result.Success(submission) : saved.ToErrorResult<Submission>();
    }
}
