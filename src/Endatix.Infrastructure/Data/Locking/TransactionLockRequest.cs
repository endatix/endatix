namespace Endatix.Infrastructure.Data.Locking;

/// <summary>
/// Which transaction lock to take, and how long to wait for it.
/// </summary>
/// <param name="Scope">
/// The kind of lock, from <see cref="TransactionLockScopes"/>. Locks of different scopes never conflict, whatever
/// their keys.
/// </param>
/// <param name="Key">What the lock guards within its scope, such as a tenant or a form.</param>
public sealed record TransactionLockRequest(int Scope, string Key)
{
    private readonly TimeSpan? _timeout;

    /// <summary>Exclusive by default. Shared locks of one key do not wait for each other, only for an exclusive one.</summary>
    public TransactionLockMode Mode { get; init; } = TransactionLockMode.Exclusive;

    /// <summary>
    /// The longest wait for the lock, or <see langword="null"/> to wait for as long as the command may run.
    /// </summary>
    public TimeSpan? Timeout
    {
        get => _timeout;
        init => _timeout = value is { } wait && wait <= TimeSpan.Zero
            ? throw new ArgumentOutOfRangeException(nameof(Timeout), value, "A lock timeout must be positive.")
            : value;
    }

    /// <inheritdoc />
    public override string ToString() => $"{Scope}:{Key}";
}

/// <summary>How a transaction lock is held.</summary>
public enum TransactionLockMode
{
    Exclusive,
    Shared,
}
