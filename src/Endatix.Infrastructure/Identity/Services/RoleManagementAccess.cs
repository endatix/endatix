using Endatix.Core.Abstractions.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Endatix.Infrastructure.Identity.Services;

/// <summary>The caller context for a role change.</summary>
public sealed class RoleManagementActor(
    IHttpContextAccessor httpContextAccessor,
    ICurrentUserAuthorizationService currentUserAuthorization)
{
    /// <summary>The current HTTP context.</summary>
    public IHttpContextAccessor HttpContextAccessor { get; } = httpContextAccessor;

    /// <summary>Authorization for the signed-in user.</summary>
    public ICurrentUserAuthorizationService CurrentUserAuthorization { get; } = currentUserAuthorization;
}

/// <summary>Shared cache and log for role changes.</summary>
public sealed class RoleManagementState(
    IAuthorizationCache authorizationCache,
    ILogger<RoleManagementState> logger)
{
    /// <summary>Authorization cache to drop after a role change.</summary>
    public IAuthorizationCache AuthorizationCache { get; } = authorizationCache;

    /// <summary>Log for role changes.</summary>
    public ILogger<RoleManagementState> Logger { get; } = logger;
}
