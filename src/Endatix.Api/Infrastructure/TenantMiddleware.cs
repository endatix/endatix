using System.Security.Claims;
using Endatix.Core.Abstractions;
using Endatix.Infrastructure.Identity;
using Endatix.Infrastructure.Identity.Authentication.Providers;
using Endatix.Infrastructure.Multitenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Endatix.Api.Infrastructure;

/// <summary>
/// Middleware that handles tenant context initialization based on user claims.
/// </summary>
/// <remarks>
/// Anonymous requests pass through without a tenant. A signed-in request must resolve a tenant greater than zero
/// through <see cref="TenantContextInitializer"/>, or it ends with 403.
/// </remarks>
public class TenantMiddleware
{
    private const string TENANT_REQUIRED_DETAIL = "The tenant for this request could not be resolved.";
    private const string NOT_APPROVED_DETAIL =
        "Your account is not approved for this tenant. Contact your administrator to be approved.";

    private readonly RequestDelegate _next;
    private readonly ILogger<TenantMiddleware> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="TenantMiddleware"/> class.
    /// </summary>
    /// <param name="next">The next middleware in the pipeline.</param>
    /// <param name="logger">The logger instance.</param>
    public TenantMiddleware(RequestDelegate next, ILogger<TenantMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    /// <summary>
    /// Processes the request to set the tenant context based on the user's tenant claim.
    /// </summary>
    /// <param name="context">The current HTTP context.</param>
    /// <param name="tenantContext">The tenant context to be initialized.</param>
    /// <param name="jwtOptions">The Endatix JWT options naming the issuers whose <c>tid</c> claim is read directly.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task InvokeAsync(HttpContext context, ITenantContext tenantContext, IOptions<EndatixJwtOptions> jwtOptions)
    {
        if (!context.User.Identities.Any(identity => identity.IsAuthenticated))
        {
            await _next(context);
            return;
        }

        switch (TenantContextInitializer.TryInitialize(tenantContext, context.User, jwtOptions.Value))
        {
            case TenantInitializationResult.Set:
                await _next(context);
                return;
            case TenantInitializationResult.NoTenant:
                LogMissingTenant(context.User);
                await WriteTenantRequiredAsync(context);
                return;
            default:
                _logger.LogError("Expected TenantContext but got {ActualType}", tenantContext.GetType().Name);
                await WriteServerErrorAsync(context);
                return;
        }
    }

    private void LogMissingTenant(ClaimsPrincipal user) =>
        _logger.LogWarning(
            "Rejected a signed-in request without a valid tenant. Issuer: {Issuer}, hydrated: {IsHydrated}",
            user.GetIssuer() ?? "unknown",
            user.IsHydrated());

    private static Task WriteTenantRequiredAsync(HttpContext context) =>
        context.WriteEndatixProblemAsync(
            StatusCodes.Status403Forbidden,
            title: null,
            detail: TenantAccessDenial.IsMarked(context) ? NOT_APPROVED_DETAIL : TENANT_REQUIRED_DETAIL,
            cancellationToken: context.RequestAborted);

    private static Task WriteServerErrorAsync(HttpContext context) =>
        context.WriteEndatixProblemAsync(
            StatusCodes.Status500InternalServerError,
            title: null,
            detail: null,
            cancellationToken: context.RequestAborted);
}
