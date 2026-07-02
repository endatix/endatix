using Endatix.Framework.Logging;
using Microsoft.Extensions.Logging;

namespace Endatix.Infrastructure.Data.Logging;

/// <summary>
/// Source-generated logging for database migration startup.
/// </summary>
internal static partial class MigrationLoggerExtensions
{
    [LoggerMessage(
        EventId = EndatixEventIds.Migrations.AutoMigrationsDisabled,
        Level = LogLevel.Information,
        Message = "Automatic database migrations are disabled")]
    public static partial void LogAutoMigrationsDisabled(this ILogger logger);

    [LoggerMessage(
        EventId = EndatixEventIds.Migrations.DbContextMigrated,
        Level = LogLevel.Warning,
        Message = "Database migrations applied for {DbContext}. Took: {DurationMs} ms.")]
    public static partial void LogDbContextMigrated(this ILogger logger, string dbContext, double durationMs);

    [LoggerMessage(
        EventId = EndatixEventIds.Migrations.DbContextNotRegistered,
        Level = LogLevel.Error,
        Message = "{DbContext} is not registered in the service provider. Startup migrations cannot run without the DbContext registration for the active provider.")]
    public static partial void LogDbContextNotRegistered(this ILogger logger, string dbContext);

    [LoggerMessage(
        EventId = EndatixEventIds.Migrations.NoMigrationsRegistered,
        Level = LogLevel.Error,
        Message = "No EF Core migrations are registered for {DbContext}. Auto-migration cannot create the database schema for the active provider. Generate provider-specific migrations before enabling startup migrations (see module README; Reporting SQL Server: https://github.com/endatix/endatix/issues/813).")]
    public static partial void LogNoMigrationsRegistered(this ILogger logger, string dbContext);
}
