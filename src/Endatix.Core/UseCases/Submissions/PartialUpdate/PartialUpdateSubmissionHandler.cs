
using System.Threading;
using System.Threading.Tasks;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.Submissions;

/// <summary>
/// Handler for partially updating a form submission.
/// </summary>
public class PartialUpdateSubmissionHandler(IRepository<Submission> repository) : ICommandHandler<PartialUpdateSubmissionCommand, Result<Submission>>
{
    private const int DEFAULT_CURRENT_PAGE = 1;

    public async Task<Result<Submission>> Handle(PartialUpdateSubmissionCommand request, CancellationToken cancellationToken)
    {
        var submission = await repository.GetByIdAsync(request.SubmissionId, cancellationToken);
        if (submission == null || submission.FormDefinition?.FormId != request.FormId)
        {
            return Result.NotFound("Form submission not found.");
        }

        // TODO: add more advanced PATCH-ing where we can not only replace individual properties, but merge, remove and other typical operations. This is valid especially for the JSON based JsonData and MetaData properties, so we can keep payloads and client logic light, e.g. submit one answer at a time and update JsonData
        // TODO: investigate if IsComplete and CurrentPage should be auto calculated as part of processing the submission
        submission.Update(
            request.JsonData ?? submission.JsonData,
            submission.FormDefinitionId,
            request.IsComplete ?? submission.IsComplete,
            request.CurrentPage?? DEFAULT_CURRENT_PAGE,
            request.Metadata ?? submission.Metadata
        );

        await repository.SaveChangesAsync(cancellationToken);

        return Result.Success(submission);
    }
}
