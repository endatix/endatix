namespace Endatix.Infrastructure.Data.Locking;

/// <summary>
/// The scope of every kind of <see cref="ITransactionLock"/>. Each kind needs a value of its own, so that its keys
/// never collide with another kind's; keeping them in one place is what keeps them apart.
/// </summary>
public static class TransactionLockScopes
{
    /// <summary>A tenant's audience match key, against the writers that add members under it.</summary>
    public const int AudienceMatchKey = 1116;

    /// <summary>One form's Reporting export schema, so that its rebuilds run one at a time.</summary>
    public const int ReportingFormSchema = 1117;
}
