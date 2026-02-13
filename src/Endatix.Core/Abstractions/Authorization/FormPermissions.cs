namespace Endatix.Core.Abstractions.Authorization;

/// <summary>
/// Defines granular, resource-specific permissions that are computed at runtime.
/// These are NOT assignable to Roles in the database.
/// </summary>
public static class FormPermissions
{
    // The specific resource we are acting upon
    public const string ResourceName = "submission";

    public static class Operations
    {
        public const string View = "view";
        public const string Edit = "edit";
        public const string Delete = "delete";
        public const string Export = "export";
        public const string UploadFile = "upload_file";
        public const string DeleteFile = "delete_file";
    }

    // Helper to generate the full string if needed (e.g., "submission.view")
    // Useful if you want to log it or send it to the frontend
    public static string View => $"{ResourceName}.{Operations.View}";
    public static string Edit => $"{ResourceName}.{Operations.Edit}";
    public static string UploadFile => $"{ResourceName}.{Operations.UploadFile}";
}