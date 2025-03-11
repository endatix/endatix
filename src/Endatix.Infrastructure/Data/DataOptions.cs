using Endatix.Framework.Configuration;
using Endatix.Infrastructure.Identity;

namespace Endatix.Infrastructure.Data;

/// <summary>
/// Configuration options for data access.
/// </summary>
public class DataOptions : EndatixOptionsBase
{
    /// <summary>
    /// Gets the section path for these options.
    /// </summary>
    public override string SectionPath => "Data";

    /// <summary>
    /// Gets or sets whether database migrations should be automatically applied at application startup.
    /// </summary>
    public bool EnableAutoMigrations { get; set; } = false;

    /// <summary>
    /// Gets or sets whether sample data (including initial user) should be seeded automatically.
    /// </summary>
    public bool SeedSampleData { get; set; } = false;

    /// <summary>
    /// Gets or sets options for the initial user.
    /// </summary>
    public InitialUserOptions? InitialUser { get; set; }
}
