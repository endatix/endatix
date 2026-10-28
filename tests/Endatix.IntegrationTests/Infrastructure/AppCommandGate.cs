using System.Data.Common;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Endatix.IntegrationTests.Infrastructure;

/// <summary>
/// Pauses one command of the host's <c>AppDbContext</c> or Audience context at a statement a test picks, whichever request runs it, so a
/// race between two HTTP requests can be forced rather than hoped for. It lets every command through until a test
/// arms it, and it pauses only the first match.
/// </summary>
/// <remarks>
/// Registered once for the shared host, which runs one test at a time. A statement must match only the request the
/// test means to pause: background work (the outbox relay, Reporting) runs through the same context.
/// </remarks>
public sealed class AppCommandGate : DbCommandInterceptor, IDbTransactionInterceptor
{
    private CommandPause? _armed;

    /// <summary>Pauses the next command whose text matches <paramref name="statement"/>, until the pause is released.</summary>
    public CommandPause PauseAt(Regex statement) => PauseAt(statement, skip: 0);

    /// <summary>
    /// Pauses the command after the next <paramref name="skip"/> commands whose text matches <paramref name="statement"/>,
    /// until the pause is released.
    /// </summary>
    public CommandPause PauseAt(Regex statement, int skip)
    {
        var pause = new CommandPause(statement, this) { Skip = skip };
        Volatile.Write(ref _armed, pause);
        return pause;
    }

    /// <summary>
    /// Pauses the commit of the first transaction that runs a command matching <paramref name="statement"/>, until the
    /// pause is released: its writes have run, uncommitted, and its locks are held.
    /// </summary>
    public CommandPause PauseAtCommitAfter(Regex statement)
    {
        var pause = new CommandPause(statement, this) { AtCommit = true };
        Volatile.Write(ref _armed, pause);
        return pause;
    }

    /// <summary>
    /// Fails the next command whose text matches <paramref name="statement"/> with <paramref name="fault"/>, as the
    /// database would, before it runs.
    /// </summary>
    public CommandPause FailAt(Regex statement, Exception fault) => FailAt(statement, fault, skip: 0);

    /// <summary>
    /// Fails the command after the next <paramref name="skip"/> commands whose text matches <paramref name="statement"/>,
    /// with <paramref name="fault"/>, as the database would, before it runs.
    /// </summary>
    public CommandPause FailAt(Regex statement, Exception fault, int skip)
    {
        var pause = new CommandPause(statement, this, fault) { Skip = skip };
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

    /// <inheritdoc />
    public async ValueTask<InterceptionResult> TransactionCommittingAsync(
        DbTransaction transaction,
        TransactionEventData eventData,
        InterceptionResult result,
        CancellationToken cancellationToken = default)
    {
        var pause = Volatile.Read(ref _armed);
        if (pause is { AtCommit: true } && pause.RanIn(transaction) && pause.TryCatch())
        {
            Disarm(pause);
            await pause.WaitForReleaseAsync(cancellationToken);
        }

        return result;
    }

    internal void Disarm(CommandPause pause) => Interlocked.CompareExchange(ref _armed, null, pause);

    private async Task HoldAsync(DbCommand command, CancellationToken cancellationToken)
    {
        var pause = Volatile.Read(ref _armed);
        if (pause is { AtCommit: true })
        {
            if (command.Transaction is { } transaction && pause.Matches(command.CommandText))
            {
                pause.MarkTransaction(transaction);
            }

            return;
        }

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
