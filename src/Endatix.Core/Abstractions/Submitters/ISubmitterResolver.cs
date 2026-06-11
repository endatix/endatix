namespace Endatix.Core.Abstractions.Submitters;

/// <summary>
/// Interface for resolving a submitter from a context.
/// </summary>
public interface ISubmitterResolver
{
    /// <summary>
    /// Looks up an existing submitter for the given context without creating or updating rows.
    /// Use for read-only paths such as public form access checks.
    /// </summary>
    /// <param name="context">The context to resolve the submitter from.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The submitter resolution when a row already exists; otherwise an empty resolution.</returns>
    Task<SubmitterResolution> FindExistingAsync(
        SubmitterResolveContext context,
        CancellationToken cancellationToken);

    /// <summary>
    /// Ensures a tenant-scoped submitter exists for the given context (upsert), then returns its resolution.
    /// Use on submission write paths where submitter linkage is warranted.
    /// </summary>
    /// <param name="context">The context to ensure the submitter from.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The submitter resolution.</returns>
    Task<SubmitterResolution> EnsureSubmitterAsync(
        SubmitterResolveContext context,
        CancellationToken cancellationToken);
}
