using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.Submissions;

/// <summary>
/// Handler for updating a form submission.
/// </summary>
public class UpdateSubmissionHandler(IRepository<Submission> repository) : ICommandHandler<UpdateSubmissionCommand, Result<Submission>>
{
    private const bool DEFAULT_IS_COMPLETE = true;
    private const int DEFAULT_CURRENT_PAGE = 1;
    private const string DEFAULT_METADATA = null;

    public async Task<Result<Submission>> Handle(UpdateSubmissionCommand request, CancellationToken cancellationToken)
    {
        var submission = await repository.GetByIdAsync(request.SubmissionId, cancellationToken);
        if (submission == null || submission.FormDefinition?.FormId != request.FormId)
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
