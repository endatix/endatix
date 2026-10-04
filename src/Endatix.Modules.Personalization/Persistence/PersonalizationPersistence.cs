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
        options.MigrationsAssembly = AssemblyName(typeof(PersonalizationDbContextBase));
        options.PostgreSqlMigrationsNamespace = MigrationsRootNamespace;
    }

    internal static string AssemblyName(Type type) =>
        type.Assembly.GetName().Name
        ?? throw new InvalidOperationException("Audience assembly name is missing.");
}
