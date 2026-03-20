using System.Collections.Immutable;

namespace Endatix.Core.Authorization.Access;

/// <summary>
/// Computed permissions for authenticated backend/admin management operations on a specific form.
/// </summary>
public sealed class FormAccessData : AccessDataBase
{
    public FormAccessData() { }

    private FormAccessData(string formId, IEnumerable<string> permissions)
    {
        FormId = formId;
        Permissions = ToImmutableSet(permissions);
    }

    public string FormId { get; init; } = string.Empty;
    public override ImmutableHashSet<string> Permissions { get; init; } = EmptyPermissions;

    public static FormAccessData CreateWithViewAccess(
        long formId) => new(formId.ToString(), ResourcePermissions.Form.Sets.ViewForm);

    public static FormAccessData CreateWithEditAccess(
        long formId) => new(formId.ToString(), ResourcePermissions.Form.Sets.EditForm);
}

