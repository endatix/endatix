using System.Diagnostics.CodeAnalysis;

namespace Endatix.Core.Configuration;

/// <summary>
/// The deployment's sender, from <c>Endatix:Integrations:Email:Sender</c>, for every email provider.
/// </summary>
/// <remarks>
/// It applies to emails rendered from a stored template, and on SMTP to an email without a From address or display
/// name. A provider template (<c>IsExternal</c>) sends the caller's From as-is. A set address wins over a template
/// row's address; only an operator's per-template <c>Endatix:EmailTemplates:*:FromAddress</c> wins over it. Left
/// empty, each template sends from its row's address. The name defaults to the deployment brand's product name.
/// </remarks>
[Experimental("ENDATIX0004")]
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
