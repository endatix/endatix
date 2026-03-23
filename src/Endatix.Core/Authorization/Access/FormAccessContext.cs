namespace Endatix.Core.Authorization.Access;

/// <summary>
/// Context for computing backend/admin management access on a specific form.
/// </summary>
public sealed class FormAccessContext
{
    public FormAccessContext(long formId)
    {
        FormId = formId;
    }

    /// <summary>
    /// The form ID.
    /// </summary>
    public long FormId { get; init; }
}

