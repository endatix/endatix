using Endatix.Infrastructure.Data;

namespace Endatix.Modules.Jobs.Persistence;

/// <summary>
/// Persistence paths and namespaces for the Background Jobs module.
/// </summary>
/// <remarks>
/// The dedicated schema is not cosmetic: <c>AddModuleDbContext</c> puts the migrations-history table
/// inside it, so job migrations advance independently of app-schema migrations. That is what makes a
/// jobs worker deployable on its own release cadence.
/// </remarks>
public static class JobsPersistence
{
    public const string Schema = "jobs";

    private const string MigrationsRootNamespace = "Endatix.Modules.Jobs.Persistence.Migrations";

    /// <summary>
    /// Shared module DbContext options for runtime and design-time registration.
    /// </summary>
    /// <remarks>
    /// Both providers' namespaces are the migrations root, never a provider folder. Registration requires the
    /// active provider's namespace, and two equal values keep namespace filtering off: this module uses
    /// provider-split DbContext types (see <see cref="JobsDbContextBase"/>), so EF already finds each provider's
    /// migrations by their <c>[DbContext]</c> attribute, leaving nothing to filter.
    /// </remarks>
    public static void ConfigureDbContextOptions(ModuleDbContextOptions options)
    {
        options.Schema = Schema;
        options.MigrationsAssembly = typeof(JobsDbContextBase).Assembly.GetName().Name!;
        options.PostgreSqlMigrationsNamespace = MigrationsRootNamespace;
        options.SqlServerMigrationsNamespace = MigrationsRootNamespace;
    }
}
