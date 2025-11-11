using System.Security.Claims;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Infrastructure.Result;
using Endatix.Infrastructure.Identity.Authorization;

namespace Endatix.Infrastructure.Identity.Authentication.Providers;

public sealed class EndatixAuthorizationProvider : IAuthorizationProvider
{
    private readonly IPermissionService? _permissionService;

    public EndatixAuthorizationProvider(IPermissionService permissionService)
    {
        _permissionService = permissionService;
    }

    /// <inheritdoc />
    public async Task<Result<AuthorizationData>> GetAuthorizationDataAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default)
    {
        var issuer = principal.GetIssuer();
        // if (issuer is null || !this.CanHandle(issuer, string.Empty))
        // {
        //     return Result.Error("Provider cannot handle the given issuer");
        // }

        var userId = principal.GetUserId();
        if (userId is null || !long.TryParse(userId, out var endatixUserId))
        {
            return Result.Error("User ID is required");
        }

        var authorizationData = await _permissionService!.GetUserPermissionsInfoAsync(endatixUserId, cancellationToken);
        if (!authorizationData.IsSuccess)
        {
            return Result.Error("Failed to get authorization data");
        }

        return Result.Success(authorizationData.Value);
    }
}