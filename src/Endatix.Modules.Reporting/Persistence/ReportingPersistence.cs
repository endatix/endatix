namespace Endatix.Modules.Reporting.Persistence;

/// <summary>
/// Persistence paths and namespaces for the Reporting module.
/// </summary>
public static class ReportingPersistence
{
    public const string Schema = "reporting";

    public const string PostgreSqlMigrationsNamespace =
        "Endatix.Modules.Reporting.Persistence.Migrations.PostgreSql";

    public const string SqlServerMigrationsNamespace =
        "Endatix.Modules.Reporting.Persistence.Migrations.SqlServer";

    public const string PostgreSqlConfigNamespace =
        "Endatix.Modules.Reporting.Persistence.Config.PostgreSql";

    public const string SqlServerConfigNamespace =
        "Endatix.Modules.Reporting.Persistence.Config.SqlServer";
}
