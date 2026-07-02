namespace Endatix.Framework.Logging;

/// <summary>
/// Platform-wide stable <see cref="Microsoft.Extensions.Logging.EventId"/> registry.
/// Grouped by area (like <see cref="Core.Abstractions.Authorization.Actions"/>).
/// Claim a range in <see cref="Ranges"/> before adding domain <c>*LoggerExtensions</c>.
/// </summary>
public static class EndatixEventIds
{
    /// <summary>Reserved numeric blocks — claim before adding IDs in a new area.</summary>
    public static class Ranges
    {
        public const int LifecycleStart = 1000;
        public const int LifecycleEnd = 1003;

        public const int MigrationsStart = 1004;
        public const int MigrationsEnd = 1099;

        public const int SeedingStart = 1100;
        public const int SeedingEnd = 1199;

        public const int HostingStart = 1200;
        public const int HostingEnd = 1299;

        public const int AuthStart = 2000;
        public const int AuthEnd = 2999;

        public const int FormsStart = 3000;
        public const int FormsEnd = 3999;

        public const int WebhooksStart = 4000;
        public const int WebhooksEnd = 4999;
    }

    /// <summary>Generic operation lifecycle (Framework <c>EndatixLoggerExtensions</c>).</summary>
    public static class Lifecycle
    {
        public const int OperationStarted = Ranges.LifecycleStart;
        public const int OperationCompleted = 1001;
        public const int OperationSkipped = 1002;
        public const int OperationFailed = Ranges.LifecycleEnd;

        public const int RangeStart = Ranges.LifecycleStart;
        public const int RangeEnd = Ranges.LifecycleEnd;

        public static readonly int[] All =
        [
            OperationStarted,
            OperationCompleted,
            OperationSkipped,
            OperationFailed
        ];
    }

    /// <summary>Database migration startup (Infrastructure <c>MigrationLoggerExtensions</c>).</summary>
    public static class Migrations
    {
        public const int AutoMigrationsDisabled = Ranges.MigrationsStart;
        public const int DbContextMigrated = 1005;
        public const int DbContextNotRegistered = 1006;
        public const int NoMigrationsRegistered = 1007;

        public const int RangeStart = Ranges.MigrationsStart;
        public const int RangeEnd = Ranges.MigrationsEnd;

        public static readonly int[] All =
        [
            AutoMigrationsDisabled,
            DbContextMigrated,
            DbContextNotRegistered,
            NoMigrationsRegistered
        ];
    }

    /// <summary>Data seeding startup (Infrastructure <c>DataSeedingLoggerExtensions</c>).</summary>
    public static class Seeding
    {
        public const int Started = Ranges.SeedingStart;
        public const int Completed = 1101;
        public const int SampleDataStarted = 1102;
        public const int SampleDataSeeded = 1103;
        public const int SampleDataFailed = 1104;

        public const int RangeStart = Ranges.SeedingStart;
        public const int RangeEnd = 1104;

        public static readonly int[] All =
        [
            Started,
            Completed,
            SampleDataStarted,
            SampleDataSeeded,
            SampleDataFailed
        ];
    }

    /// <summary>Initial identity user seeding (Infrastructure <c>IdentitySeedLoggerExtensions</c>).</summary>
    public static class IdentitySeed
    {
        public const int CredentialsInConfig = 1105;
        public const int RegistrationFailed = 1106;
        public const int RoleAssignmentFailed = 1107;
        public const int UserCreated = 1108;
        public const int PasswordInConfig = 1109;

        public const int RangeStart = CredentialsInConfig;
        public const int RangeEnd = Ranges.SeedingEnd;

        public static readonly int[] All =
        [
            CredentialsInConfig,
            RegistrationFailed,
            RoleAssignmentFailed,
            UserCreated,
            PasswordInConfig
        ];
    }

    /// <summary>Host builder startup (Hosting <c>EndatixBuilderLoggerExtensions</c>).</summary>
    public static class Hosting
    {
        public const int BuilderInitializing = Ranges.HostingStart;
        public const int BuilderInitialized = 1201;
        public const int ConfigurationStarted = 1202;
        public const int PersistenceConfigurationCompleted = 1203;
        public const int InfrastructureConfigurationCompleted = 1204;
        public const int ApiConfigurationCompleted = 1205;
        public const int HealthChecksConfigurationCompleted = 1206;
        public const int ConfigurationCompleted = 1207;
        public const int MinimalSetupCompleted = 1208;
        public const int ModuleSkippedFeatureFlag = 1209;
        public const int ModuleSkippedAlreadyRegistered = 1210;
        public const int ModuleRegistered = 1211;
        public const int FinalizingConfiguration = 1212;
        public const int MigrationContributorMissing = 1213;
        public const int ConfigurationFinalized = 1214;
        public const int DataOptionFromDefault = 1215;
        public const int DataOptionFromConfiguration = 1216;
        public const int PersistenceSetupMessage = 1217;

        public const int RangeStart = Ranges.HostingStart;
        public const int RangeEnd = Ranges.HostingEnd;

        public static readonly int[] All =
        [
            BuilderInitializing,
            BuilderInitialized,
            ConfigurationStarted,
            PersistenceConfigurationCompleted,
            InfrastructureConfigurationCompleted,
            ApiConfigurationCompleted,
            HealthChecksConfigurationCompleted,
            ConfigurationCompleted,
            MinimalSetupCompleted,
            ModuleSkippedFeatureFlag,
            ModuleSkippedAlreadyRegistered,
            ModuleRegistered,
            FinalizingConfiguration,
            MigrationContributorMissing,
            ConfigurationFinalized,
            DataOptionFromDefault,
            DataOptionFromConfiguration,
            PersistenceSetupMessage
        ];
    }

    /// <summary>All registered startup EventIds (allocated ranges only).</summary>
    public static readonly int[] AllStartup =
    [
        ..Lifecycle.All,
        ..Migrations.All,
        ..Seeding.All,
        ..IdentitySeed.All,
        ..Hosting.All
    ];

    public static bool IsInRange(int eventId, int rangeStart, int rangeEnd) =>
        eventId >= rangeStart && eventId <= rangeEnd;

    public static bool IsLifecycle(int eventId) => IsInRange(eventId, Lifecycle.RangeStart, Lifecycle.RangeEnd);

    public static bool IsMigration(int eventId) => IsInRange(eventId, Migrations.RangeStart, Migrations.RangeEnd);

    public static bool IsSeeding(int eventId) => IsInRange(eventId, Seeding.RangeStart, IdentitySeed.RangeEnd);
}
