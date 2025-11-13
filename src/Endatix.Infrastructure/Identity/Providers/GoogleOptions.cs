using System.ComponentModel.DataAnnotations;
using Endatix.Infrastructure.Identity.Authentication;

namespace Endatix.Infrastructure.Identity.Providers;
public class GoogleOptions : JwtAuthProviderOptions
{
    private const string DEFAULT_ISSUER = "https://accounts.google.com";

    public GoogleOptions()
    {
        Issuer = DEFAULT_ISSUER;
        ValidateIssuer = true;
        ValidateAudience = true;
        ValidateLifetime = true;
        ValidateIssuerSigningKey = true;
    }

    /// <summary>
    /// Google audience
    /// </summary>
    [Required]
    public string Audience { get; set; } = string.Empty;

    /// <summary>
    /// OpenID Connect metadata address
    /// </summary>
    public string MetadataAddress => $"{Issuer}/.well-known/openid-configuration";
}