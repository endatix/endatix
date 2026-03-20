namespace Endatix.Core.Authorization.Access;

/// <summary>
/// Computed permissions for authenticated backend/admin management operations on a specific form.
/// </summary>
public sealed class FormAccessData : PublicFormAccessData
{
    public static FormAccessData CreateWithViewAccess(
        long formId)
    {
        return new FormAccessData
        {
            FormId = formId.ToString(),
            SubmissionId = null,
            FormPermissions = [.. ResourcePermissions.Form.Sets.ViewForm],
            SubmissionPermissions = []
        };
    }

    public static FormAccessData CreateWithEditAccess(
        long formId)
    {
        return new FormAccessData
        {
            FormId = formId.ToString(),
            SubmissionId = null,
            FormPermissions = [.. ResourcePermissions.Form.Sets.EditForm],
            SubmissionPermissions = []
        };
    }
}

