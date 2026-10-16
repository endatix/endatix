using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.Abstractions.Forms;

/// <summary>
/// Keeps the writes that add a submission to a form apart from the form's deletion, across requests and processes.
/// Adds to one form run together. A deletion waits for the adds under way, and an add that starts during a deletion
/// is refused at once.
/// </summary>
/// <remarks>
/// <para>
/// Each call begins a transaction on the application database and takes the lock as its first statement; the lock
/// ends with the transaction. Every read and write in between joins it. Without this, a create can read the form as
/// live while its deletion has not committed, and insert a submission the deletion never sees.
/// </para>
/// <para>
/// An add re-reads the form once it holds the lock: a deletion may have committed after the add first read it.
/// </para>
/// </remarks>
public interface IFormSubmissionsLock
{
    /// <summary>For a write that adds a submission to the form.</summary>
    /// <returns>The locked section, or <see cref="ResultStatus.Unavailable"/> when the form's deletion held the lock too long.</returns>
    Task<Result<IFormSubmissionsSection>> BeginAddingAsync(long formId, CancellationToken cancellationToken);

    /// <summary>For the form's deletion.</summary>
    /// <returns>The locked section, or <see cref="ResultStatus.Unavailable"/> when adds held the lock too long.</returns>
    Task<Result<IFormSubmissionsSection>> BeginDeletingAsync(long formId, CancellationToken cancellationToken);
}
