using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Abstractions.Data;
using Endatix.Core.Entities;
using Endatix.Core.Events;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using Endatix.Core.UseCases.Identity;

namespace Endatix.Core.UseCases.Identity.AssumeTenant;

/// <summary>
/// Mints an assumed-tenant session: JWT <c>tid</c> is the target, <c>act</c> is the actor, subject stays the actor.
/// </summary>
public sealed class AssumeTenantHandler(
    IUserContext userContext,
    IUserService userService,
    IRepository<Tenant> tenantRepository,
    IUnitOfWork unitOfWork,
    IUserTokenService tokenService,
    IAuthService authService,
    ICurrentUserAuthorizationService authorizationService,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<AssumeTenantCommand, Result<AuthTokensDto>>
{
    /// <inheritdoc />
    public async Task<Result<AuthTokensDto>> Handle(AssumeTenantCommand request, CancellationToken cancellationToken)
    {
        var actor = userContext.GetCurrentUser();
        if (actor is null)
        {
            return Result.Unauthorized();
        }

        if (request.TenantId <= 0)
        {
            return Result.Invalid(new ValidationError("Tenant id is required."));
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

        var fromTenantId = actor.TenantId;
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
            return Result.Error();
        }

        tenant.RaiseContextChanged(
            user.Id,
            fromTenantId,
            tenant.Id,
            TenantContextChangedEvent.Assumed,
            dateTimeProvider.Now.UtcDateTime);
        await tenantRepository.UpdateAsync(tenant, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        await authorizationService.InvalidateAuthorizationDataCacheAsync(
            user.Id.ToString(),
            fromTenantId,
            cancellationToken);
        await authorizationService.InvalidateAuthorizationDataCacheAsync(
            user.Id.ToString(),
            tenant.Id,
            cancellationToken);

        return Result.Success(new AuthTokensDto(accessToken, refreshToken));
    }
}
