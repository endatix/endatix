namespace Endatix.Core.Exceptions;

/// <summary>
/// The database chose a write's transaction as a deadlock victim and rolled it back. Nothing the write did was kept,
/// so trying it again is safe; the caller answers that the resource is busy.
/// </summary>
public sealed class DeadlockVictimException : Exception
{
    public DeadlockVictimException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
