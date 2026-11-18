namespace Endatix.Infrastructure.Features.Authorization.PublicForm;

/// <summary>
/// Claim names and values for minimal HS256 ReBAC JWTs (resource-scoped public context, e.g. form + data lists).
/// </summary>
internal static class JwtReBacClaims
{
    /// <summary>Resource kind in this token (e.g. form, future resource types).</summary>
    internal const string RESOURCE_TYPE = "rtype";

    /// <summary>Scoped resource id (snowflake), meaning depends on <see cref="RESOURCE_TYPE"/>.</summary>
    internal const string RESOURCE_ID = "rid";

    /// <summary><see cref="RESOURCE_TYPE"/> value: form-scoped ReBAC (data lists, public form surface).</summary>
    internal const string RESOURCE_TYPE_VALUE_FORM = "form";

    /// <summary>How the token was granted. A token without it counts as <see cref="GRANT_VALUE_PUBLIC"/>.</summary>
    internal const string GRANT = "grant";

    /// <summary><see cref="GRANT"/> value: issued because the form was public.</summary>
    internal const string GRANT_VALUE_PUBLIC = "public";

    /// <summary><see cref="GRANT"/> value: issued to a caller allowed to open the private form.</summary>
    internal const string GRANT_VALUE_PRIVATE = "private";
}
