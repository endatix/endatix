using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;

namespace Endatix.Core.UseCases.Submissions;

/// <summary>
/// Handler for updating a form submission.
/// </summary>
public class UpdateSubmissionHandler(IRepository<Submission> repository) : ICommandHandler<UpdateSubmissionCommand, Result<Submission>>
{
    private const bool DEFAULT_IS_COMPLETE = false;
    private const int DEFAULT_CURRENT_PAGE = 1;
    private const string DEFAULT_METADATA = null;

    public async Task<Result<Submission>> Handle(UpdateSubmissionCommand request, CancellationToken cancellationToken)
    {
        var submissionSpec = new SubmissionByFormIdAndSubmissionIdSpec(request.FormId, request.SubmissionId);
        var submission = await repository.SingleOrDefaultAsync(submissionSpec, cancellationToken);
        if (submission == null)
        {
            return Result.NotFound("Form submission not found.");
        }

        submission.Update(
            request.JsonData,
            submission.FormDefinitionId,
            request.IsComplete ?? DEFAULT_IS_COMPLETE,
            request.CurrentPage ?? DEFAULT_CURRENT_PAGE,
            request.Metadata ?? DEFAULT_METADATA
        );

        await repository.SaveChangesAsync(cancellationToken);

        return Result.Success(submission);
    }
}
