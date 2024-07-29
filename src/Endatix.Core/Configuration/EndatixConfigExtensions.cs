using Ardalis.GuardClauses;

namespace Endatix.Core.Configuration
{
    public static class EndatixConfigExtensions
    {
        public static IEndatixConfig UseSnowflakeIds(this IEndatixConfig config, int snowflakeGeneratorId)
        {
            Guard.Against.Negative(snowflakeGeneratorId, null, "Snowflake generator id cannot be negative.");

            EndatixConfig.Configuration.UseSnowflakeIds = true;
            EndatixConfig.Configuration.SnowflakeGeneratorId = snowflakeGeneratorId;
            return config;
        }

        public static IEndatixConfig UseSqlServer(this IEndatixConfig config, string connectionString)
        {
            Guard.Against.NullOrEmpty(connectionString, null, "Endatix connection string cannot be null or empty.");

            EndatixConfig.Configuration.ConnectionString = connectionString;
            return config;
        }

        /// <summary>
        /// Adds a custom database table prefix to every DB table given <c>tablePrefix</c> argument
        /// </summary>
        /// <param name="config"></param>
        /// <param name="tablePrefix">The table prefix. If null or empty, no prefix will be added</param>
        /// <returns>The configuration</returns>
        public static IEndatixConfig UseCustomTablePrefix(this IEndatixConfig config, string tablePrefix = null)
        {
            if (!string.IsNullOrEmpty(tablePrefix))
            {
                EndatixConfig.Configuration.TablePrefix = tablePrefix;
            }

            return config;
        }

        public static IEndatixConfig UseDefaultFormDefinitionJson(this IEndatixConfig config, string defaultFormDefinitionJson)
        {
            Guard.Against.NullOrEmpty(defaultFormDefinitionJson, null, "Default form definition JSON cannot be null or empty.");

            EndatixConfig.Configuration.DefaultFormDefinitionJson = defaultFormDefinitionJson;
            return config;
        }

        public static IEndatixConfig UseSampleData(this IEndatixConfig config)
        {
            EndatixConfig.Configuration.SeedSampleData = true;
            return config;
        }
    }
}