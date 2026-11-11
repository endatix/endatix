using System.Diagnostics;
using System.Security.Claims;
using Endatix.Core.Abstractions;
using Endatix.Infrastructure.Identity;
using Endatix.Infrastructure.Identity.Authentication.Providers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Endatix.Infrastructure.Multitenancy;

/// <summary>
/// Middleware that handles tenant context initialization based on user claims.
/// </summary>
/// <remarks>
/// Anonymous requests pass through without a tenant. A signed-in request must resolve a tenant greater than zero,
/// or it ends with 403. The tenant comes from the token's own <c>tid</c> claim when Endatix issued the token (user JWT
/// or ReBAC form-access JWT), and from the hydrated <see cref="AuthorizedIdentity"/> for every other issuer.
/// </remarks>
public class TenantMiddleware
{
    private static readonly ProblemShape _tenantRequiredProblem = new(
        StatusCodes.Status403Forbidden,
        "https://www.rfc-editor.org/rfc/rfc9110.html#name-403-forbidden",
        "Forbidden access",
        "The tenant for this request could not be resolved.");

    private static readonly ProblemShape _serverErrorProblem = new(
        StatusCodes.Status500InternalServerError,
        "https://www.rfc-editor.org/rfc/rfc9110.html#name-500-internal-server-error",
        "An unexpected error occurred",
        "An unexpected error occurred");

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

        if (tenantContext is not TenantContext writableTenantContext)
        {
            _logger.LogError("Expected TenantContext but got {ActualType}", tenantContext.GetType().Name);
            await WriteProblemAsync(context, _serverErrorProblem);
            return;
        }

        var tenantId = ResolveTenantId(context.User, jwtOptions.Value);
        if (tenantId <= 0)
        {
            LogMissingTenant(context.User);
            await WriteProblemAsync(context, _tenantRequiredProblem);
            return;
        }

        writableTenantContext.SetTenant(tenantId);
        await _next(context);
    }

    private static long ResolveTenantId(ClaimsPrincipal user, EndatixJwtOptions jwtOptions)
    {
        var issuer = user.GetIssuer();
        if (issuer is not null && (issuer == jwtOptions.Issuer || issuer == jwtOptions.ReBacIssuer))
        {
            var tenantClaim = (user.Identity as ClaimsIdentity)?.FindFirst(ClaimNames.TenantId);
            return long.TryParse(tenantClaim?.Value, out var tokenTenantId) ? tokenTenantId : 0;
        }

        return user.Identities.OfType<AuthorizedIdentity>().FirstOrDefault()?.TenantId ?? 0;
    }

    private void LogMissingTenant(ClaimsPrincipal user) =>
        _logger.LogWarning(
            "Rejected a signed-in request without a valid tenant. Issuer: {Issuer}, hydrated: {IsHydrated}",
            user.GetIssuer() ?? "unknown",
            user.IsHydrated());

    private static Task WriteProblemAsync(HttpContext context, ProblemShape shape) =>
        Results.Problem(new ProblemDetails
        {
            Status = shape.Status,
            Type = shape.Type,
            Title = shape.Title,
            Detail = shape.Detail,
            Instance = context.Request.Path.Value,
            Extensions = { ["traceId"] = Activity.Current?.Id ?? context.TraceIdentifier }
        }).ExecuteAsync(context);

    /// <summary>The fields of the API's canonical ProblemDetails for one status.</summary>
    private sealed record ProblemShape(int Status, string Type, string Title, string Detail);
}
