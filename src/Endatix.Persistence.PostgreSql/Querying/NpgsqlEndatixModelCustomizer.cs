using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Endatix.Persistence.PostgreSql.Querying;

/// <summary>
/// Registers Endatix PostgreSQL DbFunctions for any context configured via the Persistence builder.
/// </summary>
public sealed class NpgsqlEndatixModelCustomizer(ModelCustomizerDependencies dependencies)
    : RelationalModelCustomizer(dependencies)
{
    /// <inheritdoc />
    public override void Customize(ModelBuilder modelBuilder, DbContext context)
    {
        base.Customize(modelBuilder, context);

        // Add support for JSON extraction. When LINQ calls NpgsqlJsonDbFunctions.ExtractObjectKeyText(json, key), translate that to the built-in SQL function jsonb_extract_path_text(...) instead of trying to run the CLR stub (which only throws). Without this mapping, JSON key search/order queries can’t be translated to SQL.
        modelBuilder.HasDbFunction(NpgsqlJsonDbFunctions.ExtractObjectKeyTextMethod)
            .HasName("jsonb_extract_path_text")
            .IsBuiltIn();
    }
}
