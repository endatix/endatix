using Endatix.Infrastructure.Data;

namespace Endatix.Modules.Audience.Persistence;

/// <summary>
/// Persistence paths for the Audience module (<c>audience</c> schema).
/// </summary>
public static class AudiencePersistence
{
    public const string Schema = "audience";

    private const string MigrationsRootNamespace =
        "Endatix.Modules.Audience.Persistence.Migrations";

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
        options.MigrationsAssembly = AssemblyName(typeof(AudienceDbContextBase));
        options.PostgreSqlMigrationsNamespace = MigrationsRootNamespace;
        options.SqlServerMigrationsNamespace = MigrationsRootNamespace;
    }

    internal static string AssemblyName(Type type) =>
        type.Assembly.GetName().Name
        ?? throw new InvalidOperationException("Audience assembly name is missing.");
}
