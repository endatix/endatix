using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Endatix.IntegrationTests;

internal sealed class SnapshotLookupPlan : DbCommandInterceptor, IDisposable
{
    private DbCommand? _lookup;
    internal bool CaptureNextRead { get; set; }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
        CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
    {
        if (CaptureNextRead)
        {
            _lookup = command.Connection!.CreateCommand();
            _lookup.CommandText = command.CommandText;
            foreach (DbParameter parameter in command.Parameters)
            {
                _lookup.Parameters.Add(((ICloneable)parameter).Clone());
            }
            CaptureNextRead = false;
        }
        return ValueTask.FromResult(result);
    }

    internal async Task<string> ExplainAsync(DatabaseFacade database, CancellationToken ct)
    {
        if (database.IsSqlServer())
        {
            await database.ExecuteSqlRawAsync("SET SHOWPLAN_XML ON", ct);
            try
            {
                return (string)(await _lookup!.ExecuteScalarAsync(ct))!;
            }
            finally
            {
                await database.ExecuteSqlRawAsync("SET SHOWPLAN_XML OFF", ct);
            }
        }
        await database.ExecuteSqlRawAsync("""ANALYZE "FormDraftOperations" """, ct);
        _lookup!.CommandText = "EXPLAIN (FORMAT JSON) " + _lookup.CommandText;
        return (string)(await _lookup.ExecuteScalarAsync(ct))!;
    }

    public void Dispose() => _lookup?.Dispose();
}
