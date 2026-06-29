using Endatix.Core.Features.Email;

namespace Endatix.Core;

/// <summary>
/// Emails which generate there body based of a Template Id
/// </summary>
public class EmailWithTemplate : BaseEmailModel
{
    /// <summary>
    /// The templateId associated with that email
    /// </summary>
    public required string TemplateId { get; init; }

    /// <summary>
    /// When false, the template is rendered from the local database.
    /// When true, the template id is sent to the external email provider.
    /// </summary>
    public bool IsExternal { get; init; }
}
