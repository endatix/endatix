using MediatR;
using Endatix.Core.Abstractions;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.Submissions.PartialUpdate;

namespace Endatix.Core.UseCases.Submissions.PartialUpdateByToken;

/// <summary>
/// Handler for partially updating a form submission by token.
/// </summary>
public class PartialUpdateSubmissionByTokenHandler(ISender sender, ISubmissionTokenService tokenService) : ICommandHandler<PartialUpdateSubmissionByTokenCommand, Result<Submission>>
{
    public async Task<Result<Submission>> Handle(PartialUpdateSubmissionByTokenCommand request, CancellationToken cancellationToken)
    {
        var tokenResult = await tokenService.ResolveTokenAsync(request.Token, cancellationToken);
        if (!tokenResult.IsSuccess)
        {
            return Result.NotFound("Invalid or expired token");
        }

        var submissionId = tokenResult.Value;

        await tokenService.ObtainTokenAsync(submissionId, cancellationToken);

        var partialUpdateSubmissionCommand = new PartialUpdateSubmissionCommand(
            submissionId,
            request.FormId,
            request.IsComplete,
            request.CurrentPage,
            request.JsonData,
            request.Metadata);
        return await sender.Send(partialUpdateSubmissionCommand, cancellationToken);
    }
}
