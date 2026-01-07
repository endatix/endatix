using Endatix.Core.Abstractions.Submissions;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;

namespace Endatix.Core.UseCases.Submissions.GetByAccessToken;

/// <summary>
/// Handler for retrieving a submission using an access token.
/// </summary>
public class GetByAccessTokenHandler(
    IRepository<Submission> submissionRepository,
    ISubmissionAccessTokenService tokenService
    ) : IQueryHandler<GetByAccessTokenQuery, Result<Submission>>
{
    public async Task<Result<Submission>> Handle(GetByAccessTokenQuery request, CancellationToken cancellationToken)
    {
        var tokenValidationResult = tokenService.ValidateAccessToken(request.Token);
        if (!tokenValidationResult.IsSuccess)
        {
            return Result.Invalid(tokenValidationResult.ValidationErrors);
        }

        var tokenClaims = tokenValidationResult.Value;

        if (!tokenClaims.Permissions.Contains(SubmissionAccessTokenPermissions.View.Name))
        {
            return Result.Forbidden("Token does not have view permission");
        }

        var submissionSpec = new SubmissionWithDefinitionSpec(request.FormId, tokenClaims.SubmissionId);
        var submission = await submissionRepository.SingleOrDefaultAsync(submissionSpec, cancellationToken);

        if (submission == null)
        {
            return Result.NotFound("Submission not found");
        }

        return Result.Success(submission);
    }
}
