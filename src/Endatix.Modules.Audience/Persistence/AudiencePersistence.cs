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

    public static void ConfigureDbContextOptions(ModuleDbContextOptions options)
    {
        options.Schema = Schema;
        options.MigrationsAssembly = AssemblyName(typeof(AudienceDbContextBase));
        options.PostgreSqlMigrationsNamespace = MigrationsRootNamespace;
    }

    internal static string AssemblyName(Type type) =>
        type.Assembly.GetName().Name
        ?? throw new InvalidOperationException("Audience assembly name is missing.");
}
