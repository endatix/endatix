using Endatix.Infrastructure.Data;

namespace Endatix.Modules.Personalization.Persistence;

/// <summary>
/// Persistence paths for the Personalization module (<c>audience</c> schema).
/// </summary>
public static class PersonalizationPersistence
{
    public const string Schema = "audience";

    private const string MigrationsRootNamespace =
        "Endatix.Modules.Personalization.Persistence.Migrations";

    public static void ConfigureDbContextOptions(ModuleDbContextOptions options)
    {
        options.Schema = Schema;
        options.MigrationsAssembly = typeof(PersonalizationDbContextBase).Assembly.GetName().Name!;
        options.PostgreSqlMigrationsNamespace = MigrationsRootNamespace;
    }
}
