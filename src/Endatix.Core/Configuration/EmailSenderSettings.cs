namespace Endatix.Core.Configuration;

/// <summary>
/// The deployment's sender, from <c>Endatix:Integrations:Email:Sender</c>. Every email provider uses it.
/// </summary>
/// <remarks>
/// The address sends an email that has no template and no From address. A template's sender is its own: an
/// operator's per-template <c>Endatix:EmailTemplates:*:FromAddress</c>, else the address the template row stores
/// (every row stores one). The name is the From display name of every email; left empty, it is the deployment
/// brand's product name.
/// </remarks>
public class EmailSenderSettings
{
    /// <summary>The configuration section.</summary>
    public const string SectionName = "Endatix:Integrations:Email:Sender";

    /// <summary>The address used when the settings leave it out.</summary>
    public const string DefaultAddress = "noreply@endatix.com";

    /// <summary>The sender address: a bare address, without a display name.</summary>
    public string Address { get; set; } = DefaultAddress;

    /// <summary>The From display name. Empty means the deployment brand's product name.</summary>
    public string? Name { get; set; }
}
