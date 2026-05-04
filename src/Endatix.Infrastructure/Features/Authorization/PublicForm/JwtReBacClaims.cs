namespace Endatix.Infrastructure.Features.Authorization.PublicForm;

/// <summary>
/// Claim names and values for minimal HS256 ReBAC JWTs (resource-scoped public context, e.g. form + data lists).
/// </summary>
internal static class JwtReBacClaims
{
    /// <summary>Resource kind in this token (e.g. form, future resource types).</summary>
    internal const string ResourceType = "rtype";

    /// <summary>Scoped resource id (snowflake), meaning depends on <see cref="ResourceType"/>.</summary>
    internal const string ResourceId = "rid";

    /// <summary><see cref="ResourceType"/> value: form-scoped ReBAC (data lists, public form surface).</summary>
    internal const string ResourceTypeValueForm = "form";
}
