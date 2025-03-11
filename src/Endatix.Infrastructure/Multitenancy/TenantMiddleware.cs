using Microsoft.AspNetCore.Http;
using Endatix.Core.Abstractions;
using Microsoft.Extensions.Logging;
using Endatix.Infrastructure.Identity;

namespace Endatix.Infrastructure.Multitenancy;

/// <summary>
/// Middleware that handles tenant context initialization based on user claims.
/// </summary>
public class TenantMiddleware
{
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
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task InvokeAsync(HttpContext context, ITenantContext tenantContext)
    {
        // Tenant ID is not present for public API endpoints.
        var tenantClaim = context.User.FindFirst(ClaimNames.TenantId);
        if (tenantClaim == null)
        {
            await _next(context);
            return;
        }

        if (!long.TryParse(tenantClaim.Value, out var tenantId))
        {
            _logger.LogError("Invalid tenant ID format: {TenantClaimValue}", tenantClaim.Value);
            await _next(context);
            return;
        }

        if (tenantContext is not TenantContext)
        {
            _logger.LogError("Expected TenantContext but got {ActualType}", tenantContext.GetType().Name);
            await _next(context);
            return;
        }

        ((TenantContext)tenantContext).SetTenant(tenantId);
        await _next(context);
    }
}
