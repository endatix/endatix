using System.Data.Common;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Endatix.IntegrationTests.Infrastructure;

/// <summary>
/// Pauses one command of the host's <c>AppDbContext</c> at a statement a test picks, whichever request runs it, so a
/// race between two HTTP requests can be forced rather than hoped for. It lets every command through until a test
/// arms it, and it pauses only the first match.
/// </summary>
/// <remarks>
/// Registered once for the shared host, which runs one test at a time. A statement must match only the request the
/// test means to pause: background work (the outbox relay, Reporting) runs through the same context.
/// </remarks>
public sealed class AppCommandGate : DbCommandInterceptor
{
    private CommandPause? _armed;

    /// <summary>Pauses the next command whose text matches <paramref name="statement"/>, until the pause is released.</summary>
    public CommandPause PauseAt(Regex statement)
    {
        var pause = new CommandPause(statement, this);
        Volatile.Write(ref _armed, pause);
        return pause;
    }

    /// <summary>
    /// Fails the next command whose text matches <paramref name="statement"/> with <paramref name="fault"/>, as the
    /// database would, before it runs.
    /// </summary>
    public CommandPause FailAt(Regex statement, Exception fault)
    {
        var pause = new CommandPause(statement, this, fault);
        pause.Release();
        Volatile.Write(ref _armed, pause);
        return pause;
    }

    /// <inheritdoc />
    public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        await HoldAsync(command, cancellationToken);
        return result;
    }

    /// <inheritdoc />
    public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        await HoldAsync(command, cancellationToken);
        return result;
    }

    internal void Disarm(CommandPause pause) => Interlocked.CompareExchange(ref _armed, null, pause);

    private async Task HoldAsync(DbCommand command, CancellationToken cancellationToken)
    {
        var pause = Volatile.Read(ref _armed);
        if (pause is not null && pause.Matches(command.CommandText) && pause.TryCatch())
        {
            Disarm(pause);
            await pause.WaitForReleaseAsync(cancellationToken);
            if (pause.Fault is { } fault)
            {
                throw fault;
            }
        }
    }
}
