namespace Endatix.Core.Abstractions.Forms;

/// <summary>
/// A section that holds an <see cref="IFormSubmissionsLock"/>. Disposing it without <see cref="CommitAsync"/> rolls
/// back everything written in it.
/// </summary>
public interface IFormSubmissionsSection : IAsyncDisposable
{
    /// <summary>Commits what the section wrote and ends the lock.</summary>
    Task CommitAsync(CancellationToken cancellationToken);
}
