namespace Endatix.Core.Authorization.Access;

/// <summary>
/// Computed permissions for authenticated backend/admin management operations on a form template.
/// </summary>
public sealed class FormTemplateAccessData : AccessDataBase
{
    /// <summary>
    /// The template ID this access data applies to.
    /// </summary>
    public string TemplateId { get; init; } = string.Empty;

    /// <summary>
    /// Permissions for the template resource.
    /// </summary>
    public override HashSet<string> Permissions { get; init; } = [];

    public static FormTemplateAccessData CreateWithViewAccess(long templateId)
    => new()
    {
        TemplateId = templateId.ToString(),
        Permissions = [.. ResourcePermissions.Template.Sets.ViewTemplate]
    };

    public static FormTemplateAccessData CreateWithEditAccess(long templateId)
    => new()
    {
        TemplateId = templateId.ToString(),
        Permissions = [.. ResourcePermissions.Template.Sets.EditTemplate]
    };
}

