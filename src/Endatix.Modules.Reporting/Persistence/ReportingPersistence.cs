using Endatix.Infrastructure.Data;

namespace Endatix.Modules.Reporting.Persistence;

/// <summary>
/// Persistence paths and namespaces for the Reporting module.
/// </summary>
public static class ReportingPersistence
{
    public const string Schema = "reporting";

    private const string MigrationsRootNamespace = "Endatix.Modules.Reporting.Persistence.Migrations";

    /// <summary>The assembly that holds every provider's Reporting migrations.</summary>
    internal static string MigrationsAssembly => typeof(ReportingDbContextBase).Assembly.GetName().Name!;

    /// <summary>
    /// Shared module DbContext options for runtime and design-time registration.
    /// </summary>
    /// <remarks>
    /// Both providers get the migrations root namespace. Registration requires a namespace for the active
    /// provider, and equal values keep namespace filtering off: each provider has its own derived context, and
    /// EF finds that context's migrations by their <c>[DbContext]</c> attribute, leaving nothing to filter.
    /// </remarks>
    public static void ConfigureDbContextOptions(ModuleDbContextOptions options)
    {
        options.Schema = Schema;
        options.MigrationsAssembly = MigrationsAssembly;
        options.PostgreSqlMigrationsNamespace = MigrationsRootNamespace;
        options.SqlServerMigrationsNamespace = MigrationsRootNamespace;
    }
}
