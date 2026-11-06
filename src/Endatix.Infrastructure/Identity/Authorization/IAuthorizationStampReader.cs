namespace Endatix.Infrastructure.Identity.Authorization;

/// <summary>
/// Reads the current <see cref="AuthorizationStamp"/> value from the database, uncached, so that every API instance
/// sees a role change on its next request.
/// </summary>
internal interface IAuthorizationStampReader
{
    /// <summary>Reads the current stamp value, or <see cref="AuthorizationStamp.Initial"/> when the row is missing.</summary>
    Task<Guid> ReadAsync(CancellationToken cancellationToken);
}
