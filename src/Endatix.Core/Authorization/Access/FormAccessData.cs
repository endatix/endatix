namespace Endatix.Core.Authorization.Access;

/// <summary>
/// Computed permissions for authenticated backend/admin management operations on a specific form.
/// </summary>
public sealed class FormAccessData : AccessDataBase
{
    public string FormId { get; init; } = string.Empty;
    public override HashSet<string> Permissions { get; init; } = [];

    public static FormAccessData CreateWithViewAccess(
        long formId) => new()
        {
            FormId = formId.ToString(),
            Permissions = [.. ResourcePermissions.Form.Sets.ViewForm]
        };

    public static FormAccessData CreateWithEditAccess(
        long formId) => new()
        {
            FormId = formId.ToString(),
            Permissions = [.. ResourcePermissions.Form.Sets.EditForm]
        };
}

