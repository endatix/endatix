using System.Security.Claims;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.UseCases.Identity.ExitAssume;
using Endatix.Infrastructure.Identity.Authentication.Providers;
using Endatix.Infrastructure.Identity.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Endatix.Infrastructure.Identity.Authentication;

/// <summary>
/// Transforms claims principal by enriching them with user permissions and roles from the database.
/// This enables FastEndpoints' built-in authorization to work with our RBAC system.
/// </summary>
internal sealed class ClaimsTransformer(
    IEnumerable<IAuthorizationStrategy> authorizationStrategies,
    IAuthorizationCache authorizationCache,
    ILogger<ClaimsTransformer> logger,
    IOptions<EndatixJwtOptions> endatixJwtOptions,
    IHttpContextAccessor? context = null) : IClaimsTransformation
{
    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal?.Identity is not ClaimsIdentity identity || !identity.IsAuthenticated)
        {
            return principal!;
        }

        if (principal.IsHydrated())
        {
            return principal;
        }

        if (string.Equals(principal.GetIssuer(), endatixJwtOptions.Value.ReBacIssuer, StringComparison.Ordinal))
        {
            return principal;
        }

        var authorizationData = await GetAuthorizationDataAsync(principal);
        if (authorizationData is not null)
        {
            principal.AddIdentity(new AuthorizedIdentity(authorizationData));
        }

        return principal;
    }

    private async Task<AuthorizationData?> GetAuthorizationDataAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default)
    {
        var userId = principal.GetUserId();
        if (userId is null)
        {
            logger.LogWarning("Authenticated principal missing user id; skipping hydrating claims with authorization data.");
            return null;
        }

        var authorizationStrategy = authorizationStrategies.FirstOrDefault(strategy => strategy.CanHandle(principal));
        if (authorizationStrategy is null)
        {
            logger.LogWarning("No authorization strategy found for issuer {Issuer}", principal.GetIssuer() ?? "unknown");
            return null;
        }

        try
        {
            if (context?.HttpContext?.GetEndpoint()?.Metadata.GetMetadata<ReturnHomeSessionMetadata>() is not null)
            {
                var grants = await authorizationStrategy.GetAuthorizationDataAsync(principal, cancellationToken);
                return grants.IsSuccess ? grants.Value with { RequiredActionsUnavailable = true, RequiredActions = [] } : null;
            }
            return await authorizationCache.ReadAsync(principal, authorizationStrategy, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Error getting authorization data for user {UserId} using strategy {AuthorizationStrategy}",
                userId,
                authorizationStrategy.GetType().Name);
            return null;
        }
    }
}
