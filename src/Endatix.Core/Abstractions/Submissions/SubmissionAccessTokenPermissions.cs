namespace Endatix.Core.Abstractions.Submissions;

/// <summary>
/// Defines temporary access permissions for submission access tokens.
/// These are simplified, scoped permissions separate from the RBAC system (Actions.*).
/// Token permissions control anonymous access via short-lived signed tokens.
/// </summary>
public static class SubmissionAccessTokenPermissions
{
    /// <summary>
    /// Represents a submission access token permission with its name and single-character encoding code.
    /// </summary>
    public record AccessTokenPermission(string Name, char Code);

    /// <summary>
    /// Permission to view submission data (read-only access).
    /// </summary>
    public static readonly AccessTokenPermission View = new("view", 'r');

    /// <summary>
    /// Permission to edit submission data (update access).
    /// </summary>
    public static readonly AccessTokenPermission Edit = new("edit", 'w');

    /// <summary>
    /// Permission to export submission data.
    /// </summary>
    public static readonly AccessTokenPermission Export = new("export", 'x');

    /// <summary>
    /// All available permissions.
    /// </summary>
    public static readonly AccessTokenPermission[] All = [View, Edit, Export];

    /// <summary>
    /// All valid permission names.
    /// </summary>
    public static readonly IEnumerable<string> AllNames = All.Select(p => p.Name);

    /// <summary>
    /// Validates if a permission name is valid (case-insensitive).
    /// </summary>
    /// <param name="permissionName">The permission name to validate</param>
    /// <returns>True if the permission is valid, false otherwise</returns>
    public static bool IsValid(string permissionName)
        => All.Any(p => p.Name.Equals(permissionName, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Gets a permission by its name (case-insensitive).
    /// </summary>
    /// <param name="name">The permission name</param>
    /// <returns>The permission object, or null if not found</returns>
    public static AccessTokenPermission? GetByName(string name)
        => All.FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Gets a permission by its code character.
    /// </summary>
    /// <param name="code">The permission code character</param>
    /// <returns>The permission object, or null if not found</returns>
    public static AccessTokenPermission? GetByCode(char code)
        => All.FirstOrDefault(p => p.Code == code);

    /// <summary>
    /// Converts a collection of permission names to permission objects.
    /// Invalid names are filtered out.
    /// </summary>
    /// <param name="names">Permission names</param>
    /// <returns>Collection of permission objects</returns>
    public static IEnumerable<AccessTokenPermission> FromNames(IEnumerable<string> names)
        => names.Select(GetByName).Where(p => p != null)!;

    /// <summary>
    /// Decodes a permissions code string (e.g., "rw") to permission objects.
    /// </summary>
    /// <param name="codesString">The code string to decode</param>
    /// <returns>Collection of permission objects</returns>
    public static IEnumerable<AccessTokenPermission> FromCodesString(string codesString)
        => codesString.Select(GetByCode).Where(p => p != null)!;

    /// <summary>
    /// Encodes a collection of permissions to a code string (e.g., "rw").
    /// </summary>
    /// <param name="permissions">Permissions to encode</param>
    /// <returns>Encoded code string</returns>
    public static string ToCodesString(IEnumerable<AccessTokenPermission> permissions)
        => string.Concat(permissions.Select(p => p.Code));

    /// <summary>
    /// Encodes permission names to a code string (e.g., ["view", "edit"] -> "rw").
    /// Invalid names are filtered out.
    /// </summary>
    /// <param name="names">Permission names to encode</param>
    /// <returns>Encoded code string</returns>
    public static string EncodeNames(IEnumerable<string> names)
        => ToCodesString(FromNames(names));

    /// <summary>
    /// Decodes a code string to permission names (e.g., "rw" -> ["view", "edit"]).
    /// </summary>
    /// <param name="codesString">Code string to decode</param>
    /// <returns>Collection of permission names</returns>
    public static IEnumerable<string> DecodeToNames(string codesString)
        => FromCodesString(codesString).Select(p => p.Name);
}
