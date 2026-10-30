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
    private readonly long? _id;

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

    /// <summary>
    /// The 64-bit identity of what the lock guards, for the one scope whose keys must never share a lock,
    /// <see cref="TransactionLockScopes.FormSubmissions"/>. PostgreSQL then locks <c>pg_advisory_xact_lock(Id)</c>, in
    /// its one-key space, which the two-key locks of every other scope never touch; nothing else in Endatix takes a
    /// one-key advisory lock. SQL Server names the resource by <see cref="Key"/> as for every scope.
    /// </summary>
    /// <exception cref="ArgumentException">Set for another scope, which would share the one-key space.</exception>
    public long? Id
    {
        get => _id;
        init => _id = Scope == TransactionLockScopes.FormSubmissions
            ? value
            : throw new ArgumentException(
                $"Only scope {TransactionLockScopes.FormSubmissions} keys its lock by a 64-bit id.", nameof(Id));
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
