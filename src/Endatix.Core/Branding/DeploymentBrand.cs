using System.Diagnostics.CodeAnalysis;

namespace Endatix.Core.Branding;

/// <summary>
/// The API's read of the deployment's brand: the built-in Endatix brand, or the mounted brand pack
/// (<c>brand.json</c> under <c>ENDATIX_BRAND_DIR</c>) when the white-labeling flag is on. Resolved once at startup.
/// </summary>
/// <remarks>
/// Identity only: who the product is and where to reach it. It names nothing about how email is sent; email reads
/// it as template variables and as the default sender name. The API does not read assets or colour tokens; the
/// Hub does. A field the pack leaves out keeps its built-in value, as the pack format specifies.
/// </remarks>
/// <param name="ProductName">The product name (the pack's <c>name</c>).</param>
/// <param name="AppName">The name of the app people sign in to: "Endatix Hub" for the built-in brand, the product name for a pack.</param>
/// <param name="AssistantName">The AI assistant's name (the pack's <c>personas.assistant</c>).</param>
/// <param name="WebsiteUrl">The product website (the pack's <c>links.website</c>).</param>
/// <param name="SupportAddress">
/// The address people are told to contact: the pack's <c>links.support</c> when that is a <c>mailto:</c> link,
/// otherwise the built-in address.
/// </param>
[Experimental("ENDATIX0003")]
public sealed record DeploymentBrand(
    string ProductName,
    string AppName,
    string AssistantName,
    string WebsiteUrl,
    string SupportAddress)
{
    /// <summary>
    /// The Endatix brand, used when no pack is mounted or the white-labeling flag is off. Its values render the
    /// seeded email templates exactly as they read before brand variables existed.
    /// </summary>
    public static DeploymentBrand BuiltIn { get; } = new(
        ProductName: "Endatix",
        AppName: "Endatix Hub",
        AssistantName: "Endatix AI Agent",
        WebsiteUrl: "https://endatix.com",
        SupportAddress: "support@endatix.com");
}
