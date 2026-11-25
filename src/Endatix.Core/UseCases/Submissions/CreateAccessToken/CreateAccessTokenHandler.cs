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
[DirectCallOnly]
public class CreateAccessTokenHandler : ICommandHandler<CreateAccessTokenCommand, Result<SubmissionAccessTokenDto>>
{
    private readonly SubmissionTargets _targets;
    private readonly ISubmissionAccessTokenService _tokenService;
    private readonly ICurrentUserAuthorizationService _authorizationService;

    /// <summary>Uses the supplied repositories for direct calls.</summary>
    public CreateAccessTokenHandler(IRepository<Submission> submissionRepository,
        ISubmissionAccessTokenService tokenService,
        ICurrentUserAuthorizationService authorizationService)
        : this(new SubmissionTargets(submissionRepository, ContentQueryScope.Repository), tokenService, authorizationService) { }

    private CreateAccessTokenHandler(SubmissionTargets targets, ISubmissionAccessTokenService tokenService, ICurrentUserAuthorizationService authorizationService)
    {
        _targets = targets;
        _tokenService = tokenService;
        _authorizationService = authorizationService;
    }

    private const string SUBMISSIONS_PREFIX = "submissions.";

    public async Task<Result<SubmissionAccessTokenDto>> Handle(CreateAccessTokenCommand request, CancellationToken cancellationToken)
    {
        foreach (var requiredPermission in request.Permissions.Select(p => SUBMISSIONS_PREFIX + p))
        {
            var accessResult = await _authorizationService.ValidateAccessAsync(requiredPermission, cancellationToken);
            if (!accessResult.IsSuccess)
            {
                return accessResult;
            }
        }

        return await _targets.CanIssueTokenAsync(request.FormId, request.SubmissionId, cancellationToken)
            ? _tokenService.GenerateAccessToken(request.SubmissionId, request.ExpiryMinutes, request.Permissions)
            : Result.NotFound("Submission not found");
    }

    internal sealed class Registered(SubmissionTargets targets, ISubmissionAccessTokenService tokenService, ICurrentUserAuthorizationService authorizationService)
        : RegisteredContentHandler<CreateAccessTokenCommand, Result<SubmissionAccessTokenDto>>(
            new CreateAccessTokenHandler(targets, tokenService, authorizationService));
}
