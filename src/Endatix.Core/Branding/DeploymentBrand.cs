namespace Endatix.Core.Branding;

/// <summary>
/// The API's read of the deployment's brand: the built-in Endatix brand, or the mounted brand pack
/// (<c>brand.json</c> under <c>ENDATIX_BRAND_DIR</c>) when the white-labeling flag is on. Resolved once at startup.
/// </summary>
/// <remarks>
/// A subset of the pack. The API does not read assets, colour tokens or links other than the website; the
/// Hub does. A field the pack leaves out keeps its built-in value, as the pack format specifies.
/// </remarks>
/// <param name="ProductName">The product name in email copy (<c>{{productName}}</c>).</param>
/// <param name="AppName">The name of the app people sign in to: "Endatix Hub" for the built-in brand, the product name for a pack.</param>
/// <param name="AssistantName">The AI assistant's name (the pack's <c>personas.assistant</c>).</param>
/// <param name="WebsiteUrl">The product website (<c>{{websiteUrl}}</c>).</param>
/// <param name="Email">The sender and support details.</param>
public sealed record DeploymentBrand(
    string ProductName,
    string AppName,
    string AssistantName,
    string WebsiteUrl,
    BrandEmail Email)
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
        Email: new BrandEmail(SenderName: null, FromAddress: null, SupportAddress: "support@endatix.com"));

    /// <summary>
    /// The brand variables every stored email template can use. A caller's metadata with the same key wins.
    /// </summary>
    public IReadOnlyDictionary<string, string> TemplateVariables => new Dictionary<string, string>
    {
        ["productName"] = ProductName,
        ["supportAddress"] = Email.SupportAddress,
        ["websiteUrl"] = WebsiteUrl,
        ["senderName"] = Email.SenderName ?? ProductName,
    };
}

/// <summary>
/// The email part of a <see cref="DeploymentBrand"/>.
/// </summary>
/// <param name="SenderName">
/// The display name on the From header. Null for the built-in brand, so each provider sends what it sent before
/// (SMTP its configured default name, Mailgun and SendGrid no name).
/// </param>
/// <param name="FromAddress">
/// The pack's sender address. It wins over a template row's address, but an operator's per-template
/// <c>Endatix:EmailTemplates:*:FromAddress</c> wins over it. Null for the built-in brand.
/// </param>
/// <param name="SupportAddress">The address people are told to contact (<c>{{supportAddress}}</c>).</param>
public sealed record BrandEmail(string? SenderName, string? FromAddress, string SupportAddress);
