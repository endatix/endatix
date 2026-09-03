using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Entities;
using Endatix.Core.Events;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;

namespace Endatix.Core.UseCases.Identity.AssumeTenant;

/// <summary>
/// Mints an assumed-tenant session: JWT <c>tid</c> is the target, <c>act</c> is the actor, subject stays the actor.
/// </summary>
public sealed class AssumeTenantHandler(
    IUserContext userContext,
    IUserService userService,
    IRepository<Tenant> tenantRepository,
    IUserTokenService tokenService,
    IAuthService authService,
    ICurrentUserAuthorizationService authorizationService,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<AssumeTenantCommand, Result<AuthTokensDto>>
{
    public const string ALREADY_IN_TENANT_MESSAGE = "You cannot assume a tenant you are already in.";

    /// <inheritdoc />
    public async Task<Result<AuthTokensDto>> Handle(AssumeTenantCommand request, CancellationToken cancellationToken)
    {
        var actor = userContext.GetCurrentUser();
        if (actor is null)
        {
            return Result.Unauthorized();
        }

        if (actor.TenantId == request.TenantId)
        {
            return Result<AuthTokensDto>.Conflict(ALREADY_IN_TENANT_MESSAGE);
        }

        // Assumed sessions do not nest: exit before switching, so `act` always names the home session.
        if (userContext.GetActorUserId() is not null)
        {
            return Result.Invalid(new ValidationError("Exit the current assumed tenant session first."));
        }

        var userResult = await userService.GetUserAsync(actor.Id, cancellationToken);
        if (!userResult.IsSuccess)
        {
            return Result.Unauthorized();
        }

        var user = userResult.Value;
        var tenant = await tenantRepository.SingleOrDefaultAsync(
            new TenantSpecifications.ByIdSpec(request.TenantId),
            cancellationToken);
        if (tenant is null)
        {
            return Result.NotFound("Tenant not found.");
        }

        var accessToken = tokenService.IssueAccessToken(
            user,
            new AccessTokenIssueOptions(
                tenant.Id,
                ActorUserId: user.Id,
                AccessExpiryMinutes: AssumeTenantSession.AccessExpiryMinutes));
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

        tenant.RaiseContextChanged(
            user.Id,
            actor.TenantId,
            tenant.Id,
            TenantContextChangedEvent.Assumed,
            dateTimeProvider.Now.UtcDateTime);
        await tenantRepository.UpdateAsync(tenant, cancellationToken);

        await authorizationService.InvalidateAuthorizationDataCacheAsync(
            user.Id.ToString(),
            tenant.Id,
            cancellationToken);

        return Result.Success(new AuthTokensDto(accessToken, refreshToken));
    }
}
