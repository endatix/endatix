namespace Endatix.Framework.Logging;

/// <summary>
/// Platform-wide stable <see cref="Microsoft.Extensions.Logging.EventId"/> registry.
/// Grouped by area (like nested classes in <see cref="Lifecycle"/>).
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

        /// <summary>Last EventId for data seeding (<see cref="Seeding"/>).</summary>
        public const int DataSeedingEnd = 1104;

        /// <summary>First EventId for identity seeding (<see cref="IdentitySeed"/>).</summary>
        public const int IdentitySeedStart = 1105;

        public const int HostingStart = 1200;
        public const int HostingEnd = 1299;

        public const int AuthStart = 2000;
        public const int AuthEnd = 2999;

        public const int FormsStart = 3000;
        public const int FormsEnd = 3999;

        public const int WebhooksStart = 4000;
        public const int WebhooksEnd = 4999;

        public const int ReCaptchaStart = 5000;
        public const int ReCaptchaEnd = 5099;

        /// <summary>First EventId reserved for email provider logging (<see cref="Email"/>).</summary>
        public const int EmailStart = 6000;
        /// <summary>Last EventId reserved for email provider logging (<see cref="Email"/>).</summary>
        public const int EmailEnd = 6099;

        /// <summary>First EventId reserved for API host logging (<see cref="Api"/>).</summary>
        public const int ApiStart = 6200;
        /// <summary>Last EventId reserved for API host logging (<see cref="Api"/>).</summary>
        public const int ApiEnd = 6299;
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

    /// <summary>
    /// Data seeding startup (Infrastructure <c>DataSeedingLoggerExtensions</c>).
    /// Partition: <see cref="Ranges.SeedingStart"/>–<see cref="Ranges.DataSeedingEnd"/>.
    /// Identity seeding uses <see cref="Ranges.IdentitySeedStart"/>–<see cref="Ranges.SeedingEnd"/> (see <see cref="IdentitySeed"/>).
    /// </summary>
    public static class Seeding
    {
        public const int Started = Ranges.SeedingStart;
        public const int Completed = 1101;
        public const int SampleDataStarted = 1102;
        public const int SampleDataSeeded = 1103;
        public const int SampleDataFailed = 1104;

        public const int RangeStart = Ranges.SeedingStart;
        public const int RangeEnd = Ranges.DataSeedingEnd;

        public static readonly int[] All =
        [
            Started,
            Completed,
            SampleDataStarted,
            SampleDataSeeded,
            SampleDataFailed
        ];
    }

    /// <summary>
    /// Initial identity user seeding (Infrastructure <c>IdentitySeedLoggerExtensions</c>).
    /// Partition: <see cref="Ranges.IdentitySeedStart"/>–<see cref="Ranges.SeedingEnd"/>.
    /// Do not assign IDs below <see cref="Ranges.IdentitySeedStart"/>; data seeding owns the lower block.
    /// </summary>
    public static class IdentitySeed
    {
        public const int CredentialsInConfig = Ranges.IdentitySeedStart;
        public const int RegistrationFailed = 1106;
        public const int RoleAssignmentFailed = 1107;
        public const int UserCreated = 1108;
        public const int PasswordInConfig = 1109;
        /// <summary>No initial user password is configured, so the initial admin is not seeded.</summary>
        public const int PasswordNotConfigured = 1110;

        public const int RangeStart = Ranges.IdentitySeedStart;
        public const int RangeEnd = Ranges.SeedingEnd;

        public static readonly int[] All =
        [
            CredentialsInConfig,
            RegistrationFailed,
            RoleAssignmentFailed,
            UserCreated,
            PasswordInConfig,
            PasswordNotConfigured
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
        public const int ModuleEndpointsNotDiscovered = 1218;
        public const int TelemetryConfigured = 1219;
        public const int TelemetrySkippedNoExporter = 1220;
        public const int LoggingConfigured = 1221;
        public const int LegacySerilogSectionDetected = 1222;
        /// <summary>Swagger middleware was added to the pipeline.</summary>
        public const int SwaggerMiddlewareAdded = 1223;
        /// <summary>Health checks middleware was added to the pipeline.</summary>
        public const int HealthChecksMiddlewareAdded = 1224;

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
            PersistenceSetupMessage,
            ModuleEndpointsNotDiscovered,
            TelemetryConfigured,
            TelemetrySkippedNoExporter,
            LoggingConfigured,
            LegacySerilogSectionDetected,
            SwaggerMiddlewareAdded,
            HealthChecksMiddlewareAdded
        ];
    }

    /// <summary>reCAPTCHA token verification (Infrastructure <c>ReCaptchaLoggerExtensions</c>).</summary>
    public static class ReCaptcha
    {
        public const int TokenRejected = Ranges.ReCaptchaStart;
        public const int TokenRejectedConfigurationError = 5001;
        public const int ScoreTooLow = 5002;
        public const int ActionMissing = 5003;

        public const int RangeStart = Ranges.ReCaptchaStart;
        public const int RangeEnd = Ranges.ReCaptchaEnd;

        public static readonly int[] All =
        [
            TokenRejected,
            TokenRejectedConfigurationError,
            ScoreTooLow,
            ActionMissing
        ];
    }

    /// <summary>Webhook delivery (Infrastructure <c>WebHookLoggerExtensions</c>).</summary>
    public static class Webhooks
    {
        /// <summary>The webhook event is disabled or not configured, so nothing is enqueued.</summary>
        public const int EventSkipped = Ranges.WebhooksStart;
        /// <summary>The webhook event has no endpoints, so nothing is enqueued.</summary>
        public const int NoEndpoints = 4001;
        /// <summary>The webhook destination URI is not a well-formed absolute URI.</summary>
        public const int InvalidUri = 4002;
        /// <summary>The webhook endpoint returned a success status code.</summary>
        public const int Delivered = 4003;
        /// <summary>The webhook endpoint returned a non-success status code.</summary>
        public const int DeliveryFailed = 4004;
        /// <summary>A single webhook attempt timed out.</summary>
        public const int DeliveryTimedOut = 4005;
        /// <summary>The resilience pipeline stopped retrying the webhook (max attempts or total timeout).</summary>
        public const int DeliveryRejected = 4006;
        /// <summary>An unexpected error occurred while delivering the webhook.</summary>
        public const int DeliveryError = 4007;
        /// <summary>The webhook background worker started.</summary>
        public const int WorkerStarted = 4008;
        /// <summary>The webhook background worker stopped.</summary>
        public const int WorkerStopped = 4009;

        /// <summary>First EventId of this group.</summary>
        public const int RangeStart = Ranges.WebhooksStart;
        /// <summary>Last EventId of this group.</summary>
        public const int RangeEnd = Ranges.WebhooksEnd;

        /// <summary>Every EventId of this group.</summary>
        public static readonly int[] All =
        [
            EventSkipped,
            NoEndpoints,
            InvalidUri,
            Delivered,
            DeliveryFailed,
            DeliveryTimedOut,
            DeliveryRejected,
            DeliveryError,
            WorkerStarted,
            WorkerStopped
        ];
    }

    /// <summary>Email provider responses (Infrastructure <c>EmailLoggerExtensions</c>).</summary>
    public static class Email
    {
        /// <summary>The email provider accepted a message.</summary>
        public const int ProviderMessageSent = Ranges.EmailStart;
        /// <summary>The email provider accepted a template message.</summary>
        public const int ProviderTemplateMessageSent = 6001;

        /// <summary>First EventId of this group.</summary>
        public const int RangeStart = Ranges.EmailStart;
        /// <summary>Last EventId of this group.</summary>
        public const int RangeEnd = Ranges.EmailEnd;

        /// <summary>Every EventId of this group.</summary>
        public static readonly int[] All =
        [
            ProviderMessageSent,
            ProviderTemplateMessageSent
        ];
    }

    /// <summary>API host concerns (Api <c>ApiLoggerExtensions</c>).</summary>
    public static class Api
    {
        /// <summary>The default CORS policy was resolved.</summary>
        public const int CorsDefaultPolicy = Ranges.ApiStart;
        /// <summary>AllowCredentials was ignored because the policy allows any origin.</summary>
        public const int CorsCredentialsIgnored = 6201;
        /// <summary>A Slack token request was received (the token is not logged).</summary>
        public const int SlackTokenReceived = 6202;

        /// <summary>First EventId of this group.</summary>
        public const int RangeStart = Ranges.ApiStart;
        /// <summary>Last EventId of this group.</summary>
        public const int RangeEnd = Ranges.ApiEnd;

        /// <summary>Every EventId of this group.</summary>
        public static readonly int[] All =
        [
            CorsDefaultPolicy,
            CorsCredentialsIgnored,
            SlackTokenReceived
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
