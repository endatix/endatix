using System.Collections.Immutable;

namespace Endatix.Core.Authorization.Access;

/// <summary>
/// Computed permissions for authenticated backend/admin management operations on a form template.
/// </summary>
public sealed class FormTemplateAccessData : AccessDataBase
{
    public FormTemplateAccessData() { }

    private FormTemplateAccessData(string templateId, IEnumerable<string> permissions)
    {
        TemplateId = templateId;
        Permissions = ToImmutableSet(permissions);
    }

    /// <summary>
    /// The template ID this access data applies to.
    /// </summary>
    public string TemplateId { get; init; } = string.Empty;

    /// <summary>
    /// Permissions for the template resource.
    /// </summary>
    public override ImmutableHashSet<string> Permissions { get; init; } = EmptyPermissions;

    public static FormTemplateAccessData CreateWithViewAccess(long templateId)
    => new(templateId.ToString(), ResourcePermissions.Template.Sets.ViewTemplate);

    public static FormTemplateAccessData CreateWithEditAccess(long templateId)
    => new(templateId.ToString(), ResourcePermissions.Template.Sets.EditTemplate);
}

