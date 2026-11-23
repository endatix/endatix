using Endatix.Core.Abstractions.Authorization;

namespace Endatix.Infrastructure.Identity;

/// <summary>
/// Recognizes the built-in Admin and PlatformAdmin roles by their system row (system-defined, at tenant 0) rather
/// than by name, since both names grant everything downstream.
/// </summary>
internal static class BuiltInAdminRoles
{
    /// <summary>Whether the name is Admin or PlatformAdmin, ignoring case.</summary>
    public static bool HasAdminName(string? roleName) =>
        string.Equals(roleName, SystemRole.Admin.Name, StringComparison.OrdinalIgnoreCase) ||
        SystemRole.IsPlatformAdminRoleName(roleName ?? string.Empty);

    /// <summary>Whether the role is a built-in system row: system-defined, at tenant 0.</summary>
    public static bool IsSystemRow(AppRole role) =>
        role.IsSystemDefined && role.TenantId <= 0;

    /// <summary>Whether the role is the built-in Admin or PlatformAdmin row.</summary>
    public static bool IsBuiltInAdminRole(AppRole role) =>
        IsSystemRow(role) && HasAdminName(role.Name);
}
