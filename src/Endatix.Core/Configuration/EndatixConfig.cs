using System;

namespace Endatix.Core.Configuration
{
    /// <summary>
    /// Legacy configuration class for Endatix.
    /// </summary>
    /// <remarks>
    /// This class is obsolete and will be removed in a future version.
    /// Use the appropriate options classes injected via DI instead:
    /// - For database settings: Use <see cref="Endatix.Infrastructure.Data.DataOptions"/> and builder methods
    /// - For Snowflake IDs: Use builder patterns for database configuration
    /// - For table prefixes: Use database provider specific options classes
    /// - For form settings: Use <see cref="Endatix.Infrastructure.Features.Submissions.SubmissionOptions"/>
    /// </remarks>
    [Obsolete(@"This class is deprecated. Use appropriate options classes injected via DI instead:
    - SeedSampleData: Use DataOptions.SeedSampleData and EnableSampleDataSeeding() on EndatixPersistenceBuilder
    - UseSnowflakeIds and SnowflakeGeneratorId: Use WithSnowflakeIds() on database provider options
    - TablePrefix: Use WithCustomTablePrefix() on database provider options
    - ConnectionString and MigrationsAssembly: Use WithConnectionString() on database provider options
    - DefaultFormDefinitionJson: Use SubmissionOptions
    
This class will be removed in a future version.")]
    public class EndatixConfig : IEndatixConfig
    {
        public static EndatixConfig Configuration { get; } = new EndatixConfig();
        
        // Database ID Generation
        /// <summary>
        /// MIGRATE TO: Use WithSnowflakeIds() method on database provider options
        /// </summary>
        public bool UseSnowflakeIds { get; set; } = false;
        
        /// <summary>
        /// MIGRATE TO: Use WithSnowflakeIds(workerId) method on database provider options
        /// </summary>
        public int SnowflakeGeneratorId { get; set; } = 0;
        
        // Database Configuration
        /// <summary>
        /// MIGRATE TO: These options are automatically handled by Entity Framework configuration
        /// </summary>
        public bool CreateDatabaseIfNotExist { get; set; } = false;
        
        /// <summary>
        /// MIGRATE TO: Use WithCustomTablePrefix() method on database provider options
        /// </summary>
        public string? TablePrefix { get; set; }
        
        /// <summary>
        /// MIGRATE TO: Use WithConnectionString() method on database provider options
        /// </summary>
        public string? ConnectionString { get; set; }

        /// <summary>
        /// MIGRATE TO: Use WithConnectionString() method on database provider options
        /// </summary>
        public string? MigrationsAssembly { get; set; }
        
        // Form Settings
        /// <summary>
        /// MIGRATE TO: Use SubmissionOptions injected via DI
        /// </summary>
        public string DefaultFormDefinitionJson { get; set; } = "{\"logoPosition\": \"right\"}";
    }
}