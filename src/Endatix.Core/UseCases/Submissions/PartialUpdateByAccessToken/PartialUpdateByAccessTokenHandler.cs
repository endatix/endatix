using MediatR;
using Endatix.Core.Abstractions.Submissions;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using Endatix.Core.UseCases.Submissions.PartialUpdate;

namespace Endatix.Core.UseCases.Submissions.PartialUpdateByAccessToken;

/// <summary>
/// Handler for partially updating a form submission by access token.
/// </summary>
public class PartialUpdateByAccessTokenHandler(
    ISender sender,
    IRepository<Submission> submissionRepository,
    ISubmissionAccessTokenService accessTokenService
    ) : ICommandHandler<PartialUpdateByAccessTokenCommand, Result<Submission>>
{
    public async Task<Result<Submission>> Handle(PartialUpdateByAccessTokenCommand request, CancellationToken cancellationToken)
    {
        var tokenValidationResult = accessTokenService.ValidateAccessToken(request.AccessToken);
        if (!tokenValidationResult.IsSuccess)
        {
            return Result.Invalid(tokenValidationResult.ValidationErrors);
        }

        var tokenClaims = tokenValidationResult.Value;

        if (!tokenClaims.Permissions.Contains(SubmissionAccessTokenPermissions.Edit.Name))
        {
            return Result.Forbidden("Token does not have edit permission");
        }

        var submissionSpec = new SubmissionWithDefinitionSpec(request.FormId, tokenClaims.SubmissionId);
        var submission = await submissionRepository.SingleOrDefaultAsync(submissionSpec, cancellationToken);

        if (submission == null)
        {
            return Result.NotFound("Submission not found");
        }

        var partialUpdateSubmissionCommand = new PartialUpdateSubmissionCommand(
            SubmissionId: tokenClaims.SubmissionId,
            FormId: request.FormId,
            IsComplete: request.IsComplete,
            CurrentPage: request.CurrentPage,
            JsonData: request.JsonData,
            Metadata: request.Metadata);

        return await sender.Send(partialUpdateSubmissionCommand, cancellationToken);
    }
}
