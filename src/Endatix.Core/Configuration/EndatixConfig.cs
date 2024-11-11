namespace Endatix.Core.Configuration
{
    public class EndatixConfig : IEndatixConfig
    {
        public static EndatixConfig Configuration { get; } = new EndatixConfig();
        public bool UseSnowflakeIds { get; set; } = false;
        public int SnowflakeGeneratorId { get; set; } = 0;
        public bool CreateDatabaseIfNotExist { get; set; } = false;
        public string? TablePrefix { get; set; }
        public string? ConnectionString { get; set; }

        public string? MigrationsAssembly { get; set; }
        public string DefaultFormDefinitionJson { get; set; } = "{\"logoPosition\": \"right\"}";
        public bool SeedSampleData { get; set; } = false;
    }
}