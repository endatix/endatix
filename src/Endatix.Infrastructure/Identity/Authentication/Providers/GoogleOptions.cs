using System.ComponentModel.DataAnnotations;

namespace Endatix.Infrastructure.Identity.Authentication.Providers;
public class GoogleOptions : JwtAuthProviderOptions
{
    internal const string DEFAULT_ISSUER = "https://accounts.google.com";

    public GoogleOptions()
    {
        ValidateIssuer = true;
        ValidateAudience = true;
        ValidateLifetime = true;
        ValidateIssuerSigningKey = true;
    }

    public override string Issuer { get; set; } = DEFAULT_ISSUER;

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