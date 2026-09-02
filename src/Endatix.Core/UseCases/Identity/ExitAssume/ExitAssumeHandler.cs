using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Entities;
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
    /// <inheritdoc />
    public async Task<Result<AuthTokensDto>> Handle(ExitAssumeCommand request, CancellationToken cancellationToken)
    {
        var actor = userContext.GetCurrentUser();
        if (actor is null)
        {
            return Result.Unauthorized();
        }

        if (userContext.GetActorUserId() is null)
        {
            return Result.Invalid(new ValidationError("Not in an assumed tenant session."));
        }

        var userResult = await userService.GetUserAsync(actor.Id, cancellationToken);
        if (!userResult.IsSuccess)
        {
            return Result.Unauthorized();
        }

        var user = userResult.Value;
        var assumedTenantId = actor.TenantId;
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

        await authorizationService.InvalidateAuthorizationDataCacheAsync(
            user.Id.ToString(),
            assumedTenantId,
            cancellationToken);
        await authorizationService.InvalidateAuthorizationDataCacheAsync(
            user.Id.ToString(),
            user.TenantId,
            cancellationToken);

        return Result.Success(new AuthTokensDto(accessToken, refreshToken));
    }
}
