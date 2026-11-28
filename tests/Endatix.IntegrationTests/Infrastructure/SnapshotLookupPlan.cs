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
            await database.ExecuteSqlRawAsync("UPDATE STATISTICS [FormDraftOperations] WITH FULLSCAN", ct);
            return await SqlServerAccessIndexesAsync(ct);
        }
        await database.ExecuteSqlRawAsync("""ANALYZE "FormDraftOperations" """, ct);
        _lookup!.CommandText = "EXPLAIN (FORMAT JSON) " + _lookup.CommandText;
        return (string)(await _lookup.ExecuteScalarAsync(ct))!;
    }

    private async Task<string> SqlServerAccessIndexesAsync(CancellationToken ct)
    {
        // STATISTICS XML returns the executed query's plan as the result set after its scalar.
        _lookup!.CommandText = $"SET STATISTICS XML ON;\n{_lookup.CommandText};\nSET STATISTICS XML OFF;";
        await using var reader = await _lookup.ExecuteReaderAsync(ct);
        (await reader.NextResultAsync(ct)).Should().BeTrue();
        (await reader.ReadAsync(ct)).Should().BeTrue();
        var plan = XDocument.Parse(reader.GetString(0));
        var objects = plan.Descendants(plan.Root!.Name.Namespace + "Object");
        return string.Join(", ", objects.Select(access => (string?)access.Attribute("Index")));
    }

    public void Dispose() => _lookup?.Dispose();
}
