using Endatix.Core.Abstractions;
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
    ICurrentUserAuthorizationService authorizationService, ITenantContext tenant
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

        var spec = new SubmissionByFormIdAndSubmissionIdSpec(request.FormId, request.SubmissionId).OnLiveForm(tenant.TenantId);
        return tenant.TenantId > 0 && await submissionRepository.AnyAsync(spec, cancellationToken)
            ? tokenService.GenerateAccessToken(request.SubmissionId, request.ExpiryMinutes, request.Permissions)
            : Result.NotFound("Submission not found");
    }
}
