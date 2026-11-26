using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Entities;
using Endatix.Core.Entities.Identity;
using Endatix.Core.Events;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;

namespace Endatix.Core.UseCases.Identity.ExitAssume;

/// <summary>
/// Issues a home-tenant session for the actor and records <c>tenant.context.changed</c> with kind exited.
/// </summary>
public sealed class ExitAssumeHandler(
    IUserContext userContext,
    IUserService userService,
    IRepository<Tenant> tenantRepository,
    IUserTokenService tokenService,
    IAuthService authService,
    ICurrentUserAuthorizationService authorizationService,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<ExitAssumeCommand, Result<AuthTokensDto>>
{
    private readonly VerifiedAssumptionReader? _verifier;

    /// <summary>Uses the built-in verifier independently of authorization-service decorators.</summary>
    [System.Diagnostics.CodeAnalysis.Experimental("ENDATIX0006")]
    public ExitAssumeHandler(IUserContext userContext, IUserService userService, IRepository<Tenant> tenantRepository,
        IUserTokenService tokenService, IAuthService authService, ICurrentUserAuthorizationService authorizationService,
        IDateTimeProvider dateTimeProvider, VerifiedAssumptionReader verifier)
        : this(userContext, userService, tenantRepository, tokenService, authService, authorizationService, dateTimeProvider)
        => _verifier = verifier;

    /// <inheritdoc />
    public async Task<Result<AuthTokensDto>> Handle(ExitAssumeCommand request, CancellationToken cancellationToken)
    {
        var actor = userContext.GetCurrentUser();
        if (actor is null && !userContext.IsAuthenticated)
        {
            return Result.Unauthorized();
        }

        var actorId = userContext.GetActorUserId();
        if (actorId is null && !userContext.HasActorClaim)
        {
            return Result.Invalid(new ValidationError("Not in an assumed tenant session."));
        }

        return actor is null || actorId != actor.Id ? Result.Forbidden() : await ExitVerifiedAsync(actor, cancellationToken);
    }

    private async Task<Result<AuthTokensDto>> ExitVerifiedAsync(User actor, CancellationToken cancellationToken)
    {
        if (_verifier is null)
        {
            return Result.Forbidden();
        }

        var proof = await _verifier.ReadAsync(actor.TenantId, cancellationToken);
        if (proof is null)
        {
            return Result.Forbidden();
        }
        var userResult = await userService.GetUserAsync(actor.Id, cancellationToken);
        if (!userResult.IsSuccess || !MatchesAssumption(proof, actor, userResult.Value))
        {
            return Result.Forbidden();
        }
        return await IssueHomeSessionAsync(userResult.Value, actor.TenantId, cancellationToken);
    }

    private static bool MatchesAssumption(VerifiedAssumption proof, User actor, User home) =>
        proof.ActorUserId == actor.Id && actor.Id == home.Id && actor.TenantId > 0
        && home.TenantId == proof.HomeTenantId && home.TenantId > 0 && home.TenantId != actor.TenantId;

    private async Task<Result<AuthTokensDto>> IssueHomeSessionAsync(User user, long assumedTenantId, CancellationToken cancellationToken)
    {
        var accessToken = tokenService.IssueAccessToken(user);
        var refreshToken = tokenService.IssueRefreshToken();
        var storeResult = await authService.StoreRefreshToken(
            user.Id,
            refreshToken.Token,
            refreshToken.ExpireAt,
            cancellationToken);
        if (!storeResult.IsSuccess)
        {
            return storeResult.ToErrorResult<AuthTokensDto>();
        }

        await RecordExitAsync(user, assumedTenantId, cancellationToken);
        await authorizationService.InvalidateAuthorizationDataCacheAsync(user.Id.ToString(), assumedTenantId, cancellationToken);
        await authorizationService.InvalidateAuthorizationDataCacheAsync(user.Id.ToString(), user.TenantId, cancellationToken);
        return Result.Success(new AuthTokensDto(accessToken, refreshToken));
    }

    private async Task RecordExitAsync(User user, long assumedTenantId, CancellationToken cancellationToken)
    {
        // Audit hangs off the assumed tenant; a deleted tenant leaves the exit unrecorded.
        var assumedTenant = await tenantRepository.SingleOrDefaultAsync(
            new TenantSpecifications.ByIdSpec(assumedTenantId),
            cancellationToken);
        if (assumedTenant is not null)
        {
            assumedTenant.RaiseContextChanged(
                user.Id,
                assumedTenantId,
                user.TenantId,
                TenantContextChangedEvent.Exited,
                dateTimeProvider.Now.UtcDateTime);
            await tenantRepository.UpdateAsync(assumedTenant, cancellationToken);
        }

    }
}
