namespace Endatix.Infrastructure.Data;

/// <summary>
/// Options for registering a module-owned <see cref="Microsoft.EntityFrameworkCore.DbContext"/>.
/// </summary>
public sealed class ModuleDbContextOptions
{
    /// <summary>
    /// Database schema for module tables and the EF migrations history table.
    /// </summary>
    public string Schema { get; set; } = "dbo";

    /// <summary>
    /// Assembly containing migrations for the module context.
    /// </summary>
    public string MigrationsAssembly { get; set; } = string.Empty;

    /// <summary>
    /// Namespace prefix for PostgreSQL migrations within <see cref="MigrationsAssembly"/>.
    /// </summary>
    public string PostgreSqlMigrationsNamespace { get; set; } = string.Empty;

    /// <summary>
    /// Namespace prefix for SQL Server migrations within <see cref="MigrationsAssembly"/>.
    /// </summary>
    public string SqlServerMigrationsNamespace { get; set; } = string.Empty;
}
