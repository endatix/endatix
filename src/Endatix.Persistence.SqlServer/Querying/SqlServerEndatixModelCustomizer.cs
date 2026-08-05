using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Endatix.Persistence.SqlServer.Querying;

/// <summary>
/// Registers Endatix SQL Server DbFunctions for any context configured via the Persistence builder.
/// </summary>
public sealed class SqlServerEndatixModelCustomizer(ModelCustomizerDependencies dependencies)
    : RelationalModelCustomizer(dependencies)
{
    /// <inheritdoc />
    public override void Customize(ModelBuilder modelBuilder, DbContext context)
    {
        base.Customize(modelBuilder, context);

        modelBuilder.HasDbFunction(SqlServerJsonDbFunctions.JsonValueMethod)
            .HasName("JSON_VALUE")
            .IsBuiltIn();
    }
}
