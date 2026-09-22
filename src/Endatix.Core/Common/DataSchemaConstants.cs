namespace Endatix.Core.Common;

/// <summary>
/// Shared max/min lengths for common persisted fields (name, email, description).
/// Email is 256 to match ASP.NET Identity <c>IdentityUser</c> /
/// <c>IdentityUserContext</c>.
/// </summary>
public static class DataSchemaConstants
{
    public const int MIN_NAME_LENGTH = 2;
    public const int MAX_NAME_LENGTH = 100;
    public const int MIN_JSON_LENGTH = 2;
    public const int MAX_DESCRIPTION_LENGTH = 500;
    public const int MAX_SLUG_LENGTH = 128;
    public const int MAX_ERROR_LENGTH = 2000;

    /// <summary>
    /// Identity maps <c>Email</c> / <c>NormalizedEmail</c> to varchar(256).
    /// </summary>
    public const int MAX_EMAIL_LENGTH = 256;
}
