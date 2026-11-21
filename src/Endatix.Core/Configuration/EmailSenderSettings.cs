namespace Endatix.Core.Configuration;

/// <summary>
/// The deployment's sender, from <c>Endatix:Integrations:Email:Sender</c>. Every email provider uses it.
/// </summary>
/// <remarks>
/// A set address is the sender of every email: it wins over a template row's address, and only an operator's
/// per-template <c>Endatix:EmailTemplates:*:FromAddress</c> wins over it. Left empty, each template sends from its
/// row's address. The name is the From display name of every email; left empty, it is the deployment brand's product
/// name.
/// </remarks>
public class EmailSenderSettings
{
    /// <summary>The configuration section.</summary>
    public const string SectionName = "Endatix:Integrations:Email:Sender";

    /// <summary>The address of an email without a template or a From address when no sender address is set.</summary>
    public const string DefaultAddress = "noreply@endatix.com";

    /// <summary>The sender address: a bare address, without a display name. Empty means none is set.</summary>
    public string? Address { get; set; }

    /// <summary>The From display name. Empty means the deployment brand's product name.</summary>
    public string? Name { get; set; }
}
