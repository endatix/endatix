namespace Endatix.Infrastructure.Identity.Authorization;

/// <summary>
/// Reads the current <see cref="AccessVersion"/> value from the database, uncached, so that every API instance sees a
/// role change on its next request.
/// </summary>
internal interface IAccessVersionReader
{
    /// <summary>Reads the current access version, or <see cref="AccessVersion.Initial"/> when the row is missing.</summary>
    Task<Guid> ReadAsync(CancellationToken cancellationToken);
}
