using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Abstractions.Submissions;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;

namespace Endatix.Core.UseCases.Submissions.CreateAccessToken;

/// <summary>
/// Handler for generating submission access tokens.
/// </summary>
public class CreateAccessTokenHandler(
    IRepository<Submission> submissionRepository,
    ISubmissionAccessTokenService tokenService,
    ICurrentUserAuthorizationService authorizationService
    ) : ICommandHandler<CreateAccessTokenCommand, Result<SubmissionAccessTokenDto>>
{
    private const string SUBMISSIONS_PREFIX = "submissions.";

    public async Task<Result<SubmissionAccessTokenDto>> Handle(CreateAccessTokenCommand request, CancellationToken cancellationToken)
    {
        foreach (var requiredPermission in request.Permissions.Select(p => SUBMISSIONS_PREFIX + p))
        {
            var accessResult = await authorizationService.ValidateAccessAsync(requiredPermission, cancellationToken);
            if (!accessResult.IsSuccess)
            {
                return accessResult;
            }
        }

        var submissionSpec = new SubmissionWithDefinitionSpec(request.FormId, request.SubmissionId);
        var submission = await submissionRepository.SingleOrDefaultAsync(submissionSpec, cancellationToken);

        if (submission == null)
        {
            return Result.NotFound("Submission not found");
        }

        return tokenService.GenerateAccessToken(
            request.SubmissionId,
            request.ExpiryMinutes,
            request.Permissions);
    }
}
