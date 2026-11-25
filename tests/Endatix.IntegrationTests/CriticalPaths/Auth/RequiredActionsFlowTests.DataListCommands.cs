using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Endatix.IntegrationTests;

public sealed partial class RequiredActionsFlowTests
{
    private sealed class DataListCommands : DbCommandInterceptor
    {
        public int ProductWork { get; set; }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            var sql = command.CommandText;
            if (sql.Contains("DataList", StringComparison.OrdinalIgnoreCase)
                && !sql.StartsWith("SELECT EXISTS", StringComparison.OrdinalIgnoreCase))
            {
                ProductWork++;
            }
            return ValueTask.FromResult(result);
        }
    }
}
