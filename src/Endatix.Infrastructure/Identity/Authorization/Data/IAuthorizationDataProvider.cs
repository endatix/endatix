using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Infrastructure.Identity.Authorization.Data;

/// <summary>
/// Provides authorization data for a user using a specific data source.
/// </summary>
public interface IAuthorizationDataProvider
{
    /// <summary>
    /// Gets authorization data for a user.
    /// </summary>
    /// <param name="userId">The ID of the user.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The authorization data for the user.</returns>
    Task<Result<AuthorizationData>> GetAuthorizationDataAsync(long userId, CancellationToken cancellationToken);
}

