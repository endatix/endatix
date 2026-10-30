using Endatix.Core.Abstractions;
using Microsoft.Extensions.Logging;

namespace Endatix.Modules.Jobs.Runtime;

/// <summary>
/// Says, at most once a minute per process, that job outcomes cannot be written because the database is read-only.
/// </summary>
/// <remarks>
/// A read-only database is an outage (<see cref="DatabaseFailures.IsReadOnly"/>): the outcome is carried and written
/// once the database takes writes again. One read-only on purpose never does, so the retries would go on without a
/// word of why beyond each failed write's exception; this names the reason without repeating it for every job.
/// </remarks>
internal sealed class ReadOnlyDatabaseWarning(IDateTimeProvider clock, ILogger<ReadOnlyDatabaseWarning> logger)
{
    internal static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

    private long _nextWarningTicks;

    /// <summary>Warns when <paramref name="failure"/> is a write a read-only database refused, unless it warned lately.</summary>
    public void WarnIfReadOnly(Exception failure)
    {
        if (!DatabaseFailures.IsReadOnly(failure))
        {
            return;
        }

        var now = clock.UtcNow.UtcTicks;
        var next = Interlocked.Read(ref _nextWarningTicks);
        if (now < next || Interlocked.CompareExchange(ref _nextWarningTicks, now + Interval.Ticks, next) != next)
        {
            return;
        }

        logger.LogWarning(
            failure,
            "Background job outcomes cannot be written: the database is read-only, as during a failover, or set read-only on purpose. They are carried and tried again until it takes writes; a database read-only on purpose never does.");
    }
}
