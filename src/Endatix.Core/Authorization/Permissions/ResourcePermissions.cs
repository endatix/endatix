namespace Endatix.Core.Authorization.Permissions;

/// <summary>
/// Defines granular permissions for form and submission resources.
/// These are computed at runtime based on context (RBAC, tokens, public access).
/// </summary>
public static class ResourcePermissions
{
    /// <summary>
    /// Form-level permissions (Context: ResourceType.Form)
    /// </summary>
    public static class Form
    {
        public const string View = "form.view";
        public const string Edit = "form.edit";
        public const string DeleteFile = "form.file.delete";
        public const string ViewFiles = "form.file.view";
        public const string UploadFile = "form.file.upload";

        /// <summary>
        /// Pre-defined sets of permissions for the form resource
        /// </summary>
        public static class Sets
        {
            /// <summary>
            /// View only permissions
            /// </summary>
            public static IReadOnlyCollection<string> ViewForm => [View, ViewFiles];

            /// <summary>
            /// Permissions required to edit a form
            /// </summary>
            public static IReadOnlyCollection<string> EditForm => [View, Edit, UploadFile, DeleteFile, ViewFiles];

            /// <summary>
            /// All permissions
            /// </summary>
            public static IReadOnlyCollection<string> All => [View, Edit, UploadFile, DeleteFile, ViewFiles];
        }
    }

    /// <summary>
    /// Submission-level permissions (Context: ResourceType.Submission)
    /// </summary>
    public static class Submission
    {
        public const string Create = "submission.create";
        public const string View = "submission.view";
        public const string Edit = "submission.edit";
        public const string Export = "submission.export";
        public const string UploadFile = "submission.file.upload";
        public const string DeleteFile = "submission.file.delete";
        public const string ViewFiles = "submission.file.view";

        /// <summary>
        /// Pre-defined sets of permissions for the submission resource
        /// </summary>
        public static class Sets
        {
            /// <summary>
            /// Permissions required to create a new submission
            /// </summary>
            public static IReadOnlyCollection<string> CreateOnly => [Create, UploadFile];

            /// <summary>
            /// Permissions required to fill in an existing submission
            /// </summary>
            public static IReadOnlyCollection<string> FillInSubmission => [View, ViewFiles, DeleteFile, UploadFile];

            /// <summary>
            /// Permissions required to review an existing submission
            /// </summary>
            public static IReadOnlyCollection<string> ReviewSubmission => [View, ViewFiles, Export];

            /// <summary>
            /// Permissions required to edit existing submission (post completion)
            /// </summary>
            public static IReadOnlyCollection<string> EditSubmission => [View, Edit, UploadFile, DeleteFile, ViewFiles];

            /// <summary>
            /// Permissions required to edit a submission
            /// </summary>
            public static IReadOnlyCollection<string> All => [Create, View, Edit, UploadFile, DeleteFile, ViewFiles];
        }
    }

    /// <summary>
    /// Gets all permissions for a given resource type.
    /// </summary>
    public static IReadOnlyCollection<string> GetAllForResourceType(string resourceType)
    {
        return resourceType switch
        {
            ResourceTypes.Form => Form.Sets.All,
            ResourceTypes.Submission => Submission.Sets.All,
            _ => []
        };
    }
}
