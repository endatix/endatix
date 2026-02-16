namespace Endatix.Core.Abstractions.Authorization;

/// <summary>
/// Defines granular permissions for form and submission resources.
/// These are computed at runtime based on context (RBAC, tokens, public access).
/// </summary>
public static class SubmissionPermissions
{
    /// <summary>
    /// Form-level permissions (Context: ResourceType.Form)
    /// </summary>
    public static class Form
    {
        public const string View = "form.view";
        public const string Design = "form.design";
    }

    /// <summary>
    /// Submission-level permissions (Context: ResourceType.Submission)
    /// </summary>
    public static class Submission
    {
        public const string Create = "submission.create";
        public const string View = "submission.view";
        public const string Edit = "submission.edit";
        public const string UploadFile = "submission.file.upload";
        public const string DeleteFile = "submission.file.delete";
        public const string ViewFiles = "submission.file.view";
    }

    /// <summary>
    /// Gets all permissions for a given resource type.
    /// </summary>
    public static string[] GetAllForResourceType(string resourceType)
    {
        return resourceType switch
        {
            ResourceTypes.Form => [Form.View, Form.Design],
            ResourceTypes.Submission => [Submission.Create, Submission.View, Submission.Edit, Submission.UploadFile, Submission.DeleteFile, Submission.ViewFiles],
            _ => []
        };
    }
}
