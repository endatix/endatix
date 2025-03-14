using System.ComponentModel.DataAnnotations;

namespace Endatix.Persistence.PostgreSql.Options;

/// <summary>
/// Options for configuring PostgreSQL database connection.
/// </summary>
public class PostgreSqlOptions
{
    /// <summary>
    /// Gets or sets the connection string for PostgreSQL.
    /// </summary>
    [Required]
    public string ConnectionString { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the assembly name where migrations are located.
    /// If not specified, the context's assembly will be used.
    /// </summary>
    public string? MigrationsAssembly { get; set; }
    
    /// <summary>
    /// Gets or sets the command timeout in seconds.
    /// </summary>
    public int? CommandTimeout { get; set; }
    
    /// <summary>
    /// Gets or sets the maximum number of retry attempts.
    /// </summary>
    public int MaxRetryCount { get; set; } = 5;
    
    /// <summary>
    /// Gets or sets the maximum delay between retries in seconds.
    /// </summary>
    public int MaxRetryDelay { get; set; } = 30;
    
    /// <summary>
    /// Gets or sets a value indicating whether to enable sensitive data logging.
    /// </summary>
    public bool EnableSensitiveDataLogging { get; set; } = false;
    
    /// <summary>
    /// Gets or sets a value indicating whether to enable detailed error messages.
    /// </summary>
    public bool EnableDetailedErrors { get; set; } = false;
    
    /// <summary>
    /// Gets or sets a value indicating whether to auto-migrate the database on startup.
    /// </summary>
    public bool AutoMigrateDatabase { get; set; } = false;
} 