using Endatix.Framework.Logging;
using Microsoft.Extensions.Logging;

namespace Endatix.Hosting.Builders.Logging;

/// <summary>
/// Source-generated logging for Endatix host builder startup.
/// </summary>
internal static partial class EndatixBuilderLoggerExtensions
{
    [LoggerMessage(
        EventId = EndatixEventIds.Hosting.BuilderInitializing,
        Level = LogLevel.Debug,
        Message = "Initializing EndatixBuilder")]
    public static partial void LogBuilderInitializing(this ILogger logger);

    [LoggerMessage(
        EventId = EndatixEventIds.Hosting.BuilderInitialized,
        Level = LogLevel.Information,
        Message = "EndatixBuilder initialized successfully")]
    public static partial void LogBuilderInitialized(this ILogger logger);

    [LoggerMessage(
        EventId = EndatixEventIds.Hosting.ConfigurationStarted,
        Level = LogLevel.Information,
        Message = "Starting Endatix configuration with default settings")]
    public static partial void LogConfigurationStarted(this ILogger logger);

    [LoggerMessage(
        EventId = EndatixEventIds.Hosting.PersistenceConfigurationCompleted,
        Level = LogLevel.Information,
        Message = "Persistence configuration completed using {DbSystem}")]
    public static partial void LogPersistenceConfigurationCompleted(this ILogger logger, string dbSystem);

    [LoggerMessage(
        EventId = EndatixEventIds.Hosting.InfrastructureConfigurationCompleted,
        Level = LogLevel.Information,
        Message = "Infrastructure configuration completed")]
    public static partial void LogInfrastructureConfigurationCompleted(this ILogger logger);

    [LoggerMessage(
        EventId = EndatixEventIds.Hosting.ApiConfigurationCompleted,
        Level = LogLevel.Information,
        Message = "API configuration completed")]
    public static partial void LogApiConfigurationCompleted(this ILogger logger);

    [LoggerMessage(
        EventId = EndatixEventIds.Hosting.HealthChecksConfigurationCompleted,
        Level = LogLevel.Information,
        Message = "Health checks configuration completed")]
    public static partial void LogHealthChecksConfigurationCompleted(this ILogger logger);

    [LoggerMessage(
        EventId = EndatixEventIds.Hosting.ConfigurationCompleted,
        Level = LogLevel.Information,
        Message = "Endatix configuration completed successfully")]
    public static partial void LogConfigurationCompleted(this ILogger logger);

    [LoggerMessage(
        EventId = EndatixEventIds.Hosting.MinimalSetupCompleted,
        Level = LogLevel.Information,
        Message = "Minimal setup completed with {DbSystem} persistence")]
    public static partial void LogMinimalSetupCompleted(this ILogger logger, string dbSystem);

    [LoggerMessage(
        EventId = EndatixEventIds.Hosting.ModuleSkippedFeatureFlag,
        Level = LogLevel.Debug,
        Message = "Skipped module {AssemblyName}: feature flag {FeatureFlag} is disabled")]
    public static partial void LogModuleSkippedFeatureFlag(
        this ILogger logger,
        string assemblyName,
        string? featureFlag);

    [LoggerMessage(
        EventId = EndatixEventIds.Hosting.ModuleSkippedAlreadyRegistered,
        Level = LogLevel.Debug,
        Message = "Skipped module {AssemblyName}: already registered")]
    public static partial void LogModuleSkippedAlreadyRegistered(this ILogger logger, string assemblyName);

    [LoggerMessage(
        EventId = EndatixEventIds.Hosting.ModuleRegistered,
        Level = LogLevel.Debug,
        Message = "Registered module from assembly {AssemblyName}")]
    public static partial void LogModuleRegistered(this ILogger logger, string assemblyName);

    [LoggerMessage(
        EventId = EndatixEventIds.Hosting.FinalizingConfiguration,
        Level = LogLevel.Debug,
        Message = "Finalizing all configurations")]
    public static partial void LogFinalizingConfiguration(this ILogger logger);

    [LoggerMessage(
        EventId = EndatixEventIds.Hosting.MigrationContributorMissing,
        Level = LogLevel.Warning,
        Message = "Module {AssemblyName} implements IHasDbMigrations but did not register a migration contributor via AddDbContextWithMigrations")]
    public static partial void LogMigrationContributorMissing(this ILogger logger, string assemblyName);

    [LoggerMessage(
        EventId = EndatixEventIds.Hosting.ConfigurationFinalized,
        Level = LogLevel.Information,
        Message = "All configurations have been finalized")]
    public static partial void LogConfigurationFinalized(this ILogger logger);

    [LoggerMessage(
        EventId = EndatixEventIds.Hosting.DataOptionFromDefault,
        Level = LogLevel.Debug,
        Message = "Setting {SettingName}={SettingValue} from code default")]
    public static partial void LogDataOptionFromDefault(
        this ILogger logger,
        string settingName,
        string settingValue);

    [LoggerMessage(
        EventId = EndatixEventIds.Hosting.DataOptionFromConfiguration,
        Level = LogLevel.Debug,
        Message = "Using {SettingName}={SettingValue} value from configuration")]
    public static partial void LogDataOptionFromConfiguration(
        this ILogger logger,
        string settingName,
        string settingValue);

    [LoggerMessage(
        EventId = EndatixEventIds.Hosting.PersistenceSetupMessage,
        Level = LogLevel.Debug,
        Message = "[Persistence Setup] {Message}")]
    public static partial void LogPersistenceSetup(this ILogger logger, string message);
}
