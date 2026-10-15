using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;

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

        // SPIKE: lax JSON_VALUE returns NULL above 4,000 characters; RETURNING nvarchar(max) lifts the limit.
        modelBuilder.HasDbFunction(SqlServerJsonDbFunctions.JsonValueMaxMethod)
            .HasTranslation(args =>
            {
                var path = (string)((SqlConstantExpression)args[1]).Value!;
                var fragment = new SqlFragmentExpression($"N'{path.Replace("'", "''", StringComparison.Ordinal)}' RETURNING nvarchar(max)");
                return new SqlFunctionExpression(
                    "JSON_VALUE",
                    [args[0], fragment],
                    nullable: true,
                    argumentsPropagateNullability: [true, false],
                    typeof(string),
                    args[1].TypeMapping);
            });
    }
}
