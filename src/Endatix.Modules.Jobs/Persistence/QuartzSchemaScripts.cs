namespace Endatix.Modules.Jobs.Persistence;

/// <summary>
/// The scheduler's DDL, embedded so a migration applies exactly the script that was reviewed with it.
/// </summary>
internal static class QuartzSchemaScripts
{
    /// <summary>Quartz.NET 4.2.2's PostgreSQL tables, in the <c>jobs</c> schema.</summary>
    public const string PostgreSqlTables = "Quartz_4_2_2_Tables.sql";

    /// <summary>
    /// Every scheduler table, children first, so they can be dropped in order.
    /// </summary>
    public static readonly string[] TablesInDropOrder =
    [
        "qrtz_fired_triggers",
        "qrtz_paused_trigger_grps",
        "qrtz_paused_job_grps",
        "qrtz_scheduler_state",
        "qrtz_locks",
        "qrtz_simprop_triggers",
        "qrtz_simple_triggers",
        "qrtz_cron_triggers",
        "qrtz_blob_triggers",
        "qrtz_triggers",
        "qrtz_job_details",
        "qrtz_calendars",
        "qrtz_execution_history",
        "qrtz_misfire_history",
    ];

    public static string Read(string scriptName)
    {
        var assembly = typeof(QuartzSchemaScripts).Assembly;
        var resourceName = assembly.GetManifestResourceNames()
            .Single(name => name.EndsWith("." + scriptName, StringComparison.Ordinal));

        using var stream = assembly.GetManifestResourceStream(resourceName)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
