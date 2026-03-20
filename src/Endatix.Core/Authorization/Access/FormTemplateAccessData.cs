namespace Endatix.Core.Authorization.Access;

/// <summary>
/// Computed permissions for authenticated backend/admin management operations on a form template.
/// </summary>
public sealed class FormTemplateAccessData : IAccessData
{
    /// <summary>
    /// The template ID this access data applies to.
    /// </summary>
    public string TemplateId { get; init; } = string.Empty;

    /// <summary>
    /// Permissions for the template resource.
    /// </summary>
    public HashSet<string> Permissions { get; init; } = [];

    /// <inheritdoc/>
    public DateTimeOffset? ExpiresAt { get; init; }

    public bool Has(string permission)
    {
        return Permissions.Contains(permission);
    }

    public bool HasAny(IEnumerable<string> permissions)
    {
        return permissions.Any(Has);
    }

    public bool HasAll(IEnumerable<string> permissions)
    {
        return permissions.All(Has);
    }

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

