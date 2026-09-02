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

namespace Endatix.Core.UseCases.Identity.SwitchTenant;

/// <summary>
/// Membership switch: JWT <c>tid</c> becomes the target tenant, no <c>act</c> claim, last-used TenantId is updated.
/// </summary>
public sealed class SwitchTenantHandler(
    IUserContext userContext,
    IUserService userService,
    IRepository<Tenant> tenantRepository,
    IUnitOfWork unitOfWork,
    IUserTokenService tokenService,
    IAuthService authService,
    ICurrentUserAuthorizationService authorizationService,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<SwitchTenantCommand, Result<AuthTokensDto>>
{
    /// <inheritdoc />
    public async Task<Result<AuthTokensDto>> Handle(SwitchTenantCommand request, CancellationToken cancellationToken)
    {
        var actor = userContext.GetCurrentUser();
        if (actor is null)
        {
            return Result.Unauthorized();
        }

        if (userContext.GetActorUserId() is not null)
        {
            return Result.Invalid(new ValidationError("Exit assumed tenant before switching."));
        }

        if (request.TenantId <= 0)
        {
            return Result.Invalid(new ValidationError("Tenant id is required."));
        }

        var fromTenantId = actor.TenantId;
        var userResult = await userService.SetActiveTenantAsync(actor.Id, request.TenantId, cancellationToken);
        if (!userResult.IsSuccess)
        {
            return userResult.ToErrorResult<AuthTokensDto>();
        }

        var user = userResult.Value;
        var tenant = await tenantRepository.SingleOrDefaultAsync(
            new TenantSpecifications.ByIdSpec(request.TenantId),
            cancellationToken);
        if (tenant is null)
        {
            return Result.NotFound("Tenant not found.");
        }

        var accessToken = tokenService.IssueAccessToken(user);
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
            TenantContextChangedEvent.KindSwitched,
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
