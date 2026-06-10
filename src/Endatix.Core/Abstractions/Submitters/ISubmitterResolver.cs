namespace Endatix.Core.Abstractions.Submitters;

/// <summary>
/// Interface for resolving a submitter from a context.
/// </summary>
public interface ISubmitterResolver
{
    /// <summary>
    /// Resolves a submitter from a context.
    /// </summary>
    /// <param name="context">The context to resolve the submitter from.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The submitter resolution.</returns>
    Task<SubmitterResolution> ResolveAsync(
        SubmitterResolveContext context,
        CancellationToken cancellationToken);
}
