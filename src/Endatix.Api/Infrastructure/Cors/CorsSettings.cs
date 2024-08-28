using Endatix.Framework.Settings;

namespace Endatix.Api.Infrastructure.Cors;

/// <summary>
/// CORS Settings for the Endatix app. Designed to be stored in the appSettings.json config files
/// </summary>
public class CorsSettings : IEndatixSettings
{
    /// <summary>
    /// Optional string. Use it to set the default CORS policy name.
    /// NOTE: if you leave empty, there will be auto-assignment of the default policy based of the first CorsPolicy available or fallback to the default policies registered with the system, which are AllowAll for development and Disallow all for non-development environments
    /// </summary>
    public string? DefaultPolicyName { get; set; }

    /// <summary>
    /// List of CORS Policies using the <see cref="CorsPolicySetting"/> class
    /// </summary>
    public IList<CorsPolicySetting> CorsPolicies { get; set; } = [];
}
