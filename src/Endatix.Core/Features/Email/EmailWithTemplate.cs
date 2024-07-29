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
}
