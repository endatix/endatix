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
    ICurrentUserAuthorizationService authorizationService, ManagedSubmissionAccess? access
    ) : ICommandHandler<CreateAccessTokenCommand, Result<SubmissionAccessTokenDto>>
{
    private const string SUBMISSIONS_PREFIX = "submissions.";

    public CreateAccessTokenHandler(IRepository<Submission> submissionRepository, ISubmissionAccessTokenService tokenService,
        ICurrentUserAuthorizationService authorizationService) : this(submissionRepository, tokenService, authorizationService, null) { }

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

        var refusal = await CheckTargetAsync(request, cancellationToken);
        return refusal is null ? tokenService.GenerateAccessToken(request.SubmissionId, request.ExpiryMinutes, request.Permissions)
            : refusal.ToErrorResult<SubmissionAccessTokenDto>();
    }

    private async Task<Result?> CheckTargetAsync(CreateAccessTokenCommand request, CancellationToken ct)
    {
        if (access is { IsEnforced: true })
        {
            return await access.SubmissionAsync((request.FormId, request.SubmissionId), "Submission not found", ct);
        }
        var spec = new SubmissionWithDefinitionSpec(request.FormId, request.SubmissionId);
        return await submissionRepository.SingleOrDefaultAsync(spec, ct) is null ? Result.NotFound("Submission not found") : null;
    }
}
