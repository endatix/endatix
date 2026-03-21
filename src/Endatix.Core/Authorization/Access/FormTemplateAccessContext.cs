namespace Endatix.Core.Authorization.Access;

/// <summary>
/// Context for computing backend/admin management access on a specific form template.
/// </summary>
public sealed class FormTemplateAccessContext
{
    public FormTemplateAccessContext(long templateId)
    {
        TemplateId = templateId;
    }

    /// <summary>
    /// The form template ID.
    /// </summary>
    public long TemplateId { get; init; }
}

