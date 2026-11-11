using Endatix.Core.Abstractions.Authorization;
using Microsoft.Extensions.Logging;

namespace Endatix.Infrastructure.Identity.Users;

/// <summary>Cache and log used while changing a user.</summary>
public sealed class AppUserAudit(
    IAuthorizationCache authorizationCache,
    ILogger<AppUserAudit> logger)
{
    /// <summary>Authorization cache to drop after a user change.</summary>
    public IAuthorizationCache AuthorizationCache { get; } = authorizationCache;

    /// <summary>Log for user changes.</summary>
    public ILogger<AppUserAudit> Logger { get; } = logger;
}
