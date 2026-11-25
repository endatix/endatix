using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Endatix.IntegrationTests;

public sealed partial class RequiredActionsFlowTests
{
    private sealed class DataListCommands
    {
        public int ProductWork { get; set; }
        public int FormWork { get; private set; }

        public void Observe(DbContextOptionsBuilder options) =>
            options.LogTo((id, _) => id == RelationalEventId.CommandExecuting,
                e => Record(((CommandEventData)e).Command.CommandText));

        private void Record(string sql)
        {
            if (ReferencesTable(sql, "Forms") || ReferencesTable(sql, "FormDependencies"))
            {
                FormWork++;
            }
            if (sql.Contains("DataList", StringComparison.OrdinalIgnoreCase))
            {
                ProductWork++;
            }
        }

        private static bool ReferencesTable(string sql, string table) =>
            sql.Contains($"\"{table}\"", StringComparison.OrdinalIgnoreCase)
            || sql.Contains($"[{table}]", StringComparison.OrdinalIgnoreCase);
    }
}
