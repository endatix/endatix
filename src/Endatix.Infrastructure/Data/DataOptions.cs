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
    /// Gets or sets whether migrations should be applied automatically.
    /// </summary>
    public bool ApplyMigrations { get; set; } = false;

    /// <summary>
    /// Gets or sets options for the initial user.
    /// </summary>
    public InitialUserOptions? InitialUser { get; set; }
}
