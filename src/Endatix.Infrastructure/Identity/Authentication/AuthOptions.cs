using Endatix.Infrastructure.Builders;

namespace Endatix.Infrastructure.Identity.Authentication;

public class AuthOptions
{
    /// <summary>
    /// The configuration section name where these options are stored
    /// </summary>
    public const string SECTION_NAME = "Endatix:Auth";

    /// <summary>
    /// The default scheme to use for authentication
    /// </summary>
    public string DefaultScheme { get; set; } = InfrastructureSecurityBuilder.MULTI_JWT_SCHEME_NAME;

    /// <summary>
    /// Dynamic provider configurations - allows any provider to register its config
    /// </summary>
    public Dictionary<string, object> Providers { get; set; } = new();
}