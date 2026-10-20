using Endatix.Core.Exceptions;
using Microsoft.Data.SqlClient;
using Npgsql;

namespace Endatix.Infrastructure.Data;

/// <summary>Tells, from what the driver reports, that the database rolled a transaction back to end a deadlock.</summary>
internal static class DatabaseDeadlocks
{
    private const int SqlServerDeadlockVictim = 1205;

    /// <summary>
    /// Whether <paramref name="exception"/>, or one it wraps, is the deadlock victim's error. A save reports it inside
    /// a <c>DbUpdateException</c>, which the provider's execution strategy wraps again in an
    /// <see cref="InvalidOperationException"/>, as it does every transient failure.
    /// </summary>
    public static bool IsVictim(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is PostgresException { SqlState: PostgresErrorCodes.DeadlockDetected }
                || current is SqlException { Number: SqlServerDeadlockVictim })
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>The Core exception for a deadlock victim, which the writers answer as busy rather than failed.</summary>
    public static DeadlockVictimException Victim(Exception exception) =>
        new("The write was chosen as a deadlock victim and rolled back.", exception);
}
