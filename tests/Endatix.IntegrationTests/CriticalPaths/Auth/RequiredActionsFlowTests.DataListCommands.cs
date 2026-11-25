using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Endatix.IntegrationTests;

public sealed partial class RequiredActionsFlowTests
{
    private sealed class DataListCommands
    {
        public int ProductWork { get; set; }

        public void Observe(DbContextOptionsBuilder options) =>
            options.LogTo((id, _) => id == RelationalEventId.CommandExecuting,
                e => Record(((CommandEventData)e).Command.CommandText));

        private void Record(string sql)
        {
            if (sql.Contains("DataList", StringComparison.OrdinalIgnoreCase)
                && !sql.Contains("-- Managed data list visibility", StringComparison.Ordinal))
            {
                ProductWork++;
            }
        }
    }
}
