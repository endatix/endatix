using Endatix.Infrastructure.Data.Querying;
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

        modelBuilder.HasDbFunction(JsonDbFunctions.KeyTextMethod)
            .HasTranslation(arguments => new SqlFunctionExpression(
                "JSON_VALUE",
                [arguments[0], KeyPath(arguments[1])],
                nullable: true,
                argumentsPropagateNullability: [true, false],
                typeof(string),
                typeMapping: null));
    }

    /// <summary>JSON_VALUE takes a path, not a key: the constant key becomes <c>$."key"</c>.</summary>
    private static SqlConstantExpression KeyPath(SqlExpression key) =>
        key is SqlConstantExpression { Value: string name } constant
            ? new SqlConstantExpression(
                SqlServerJsonObjectKeyFilter.BuildJsonValuePath(name), typeof(string), constant.TypeMapping)
            : throw new InvalidOperationException(
                $"{nameof(JsonDbFunctions.KeyText)} on SQL Server needs a constant key.");
}
