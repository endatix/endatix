using System.Data.Common;
using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Endatix.IntegrationTests;

internal sealed class SnapshotLookupPlan : DbCommandInterceptor, IDisposable
{
    private DbCommand? _lookup;
    internal bool CaptureNextRead { get; set; }

    public override DbCommand CommandInitialized(CommandEndEventData eventData, DbCommand command)
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
        return command;
    }

    internal async Task<string> ExplainAsync(DatabaseFacade database, CancellationToken ct)
    {
        if (database.IsSqlServer())
        {
            await database.ExecuteSqlRawAsync("SET SHOWPLAN_XML ON", ct);
            try
            {
                var xml = (string)(await _lookup!.ExecuteScalarAsync(ct))!;
                var plan = XDocument.Parse(xml);
                var objects = plan.Descendants(plan.Root!.Name.Namespace + "Object");
                return string.Join(", ", objects.Select(access => (string?)access.Attribute("Index")));
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
