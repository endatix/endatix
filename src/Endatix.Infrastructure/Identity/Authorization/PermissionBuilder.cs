using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Entities.Identity;

namespace Endatix.Infrastructure.Identity.Authorization;


/// <summary>
/// Helper methods for building permission strings dynamically
/// </summary>
public static class PermissionBuilder
{
    /// <summary>
    /// Builds a permission string using category and action
    /// </summary>
    /// <param name="category">The permission category</param>
    /// <param name="action">The action type</param>
    /// <returns>Permission string in format: {category}.{action}</returns>
    public static string Build(PermissionCategory category, string action)
    {
        return $"{category.Code}.{action}";
    }

    /// <summary>
    /// Builds a permission string using category, action, and scope
    /// </summary>
    /// <param name="category">The permission category</param>
    /// <param name="action">The action type</param>
    /// <param name="scope">The scope</param>
    /// <returns>Permission string in format: {category}.{action}.{scope}</returns>
    public static string Build(PermissionCategory category, string action, string scope)
    {
        return $"{category.Code}.{action}.{scope}";
    }

    /// <summary>
    /// Builds a permission string using category, subcategory, and action
    /// </summary>
    /// <param name="category">The permission category</param>
    /// <param name="subcategory">The subcategory (e.g., "users", "roles")</param>
    /// <param name="action">The action type</param>
    /// <returns>Permission string in format: {category}.{subcategory}.{action}</returns>
    public static string BuildWithSubcategory(PermissionCategory category, string subcategory, string action)
    {
        return $"{category.Code}.{subcategory}.{action}";
    }


    /// <summary>
    /// Gets all permission constants defined in this class
    /// </summary>
    /// <returns>Collection of all permission strings</returns>
    public static IEnumerable<string> GetAllPermissions()
    {
        var permissions = new List<string>();

        // Use reflection to get all const string fields from nested classes
        var permissionClasses = typeof(Actions).GetNestedTypes()
            .Where(t => t.IsClass && t.IsSealed);

        foreach (var permissionClass in permissionClasses)
        {
            var constants = permissionClass.GetFields()
                .Where(f => f.IsLiteral && f.FieldType == typeof(string))
                .Select(f => f.GetValue(null)?.ToString())
                .Where(v => !string.IsNullOrEmpty(v));

            permissions.AddRange(constants!);
        }

        return permissions.OrderBy(p => p);
    }

    /// <summary>
    /// Gets permissions by category (supports both PermissionCategory objects and strings)
    /// </summary>
    /// <param name="category">The category</param>
    /// <returns>Collection of permissions for the specified category</returns>
    public static IEnumerable<string> GetPermissionsByCategory(PermissionCategory category)
    {
        return GetAllPermissions()
            .Where(p => p.StartsWith($"{category.Code}.", StringComparison.OrdinalIgnoreCase))
            .OrderBy(p => p);
    }

    /// <summary>
    /// Gets permissions by category code
    /// </summary>
    /// <param name="categoryCode">The category code (e.g., "forms", "submissions")</param>
    /// <returns>Collection of permissions for the specified category</returns>
    public static IEnumerable<string> GetPermissionsByCategory(string categoryCode)
    {
        return GetAllPermissions()
            .Where(p => p.StartsWith($"{categoryCode.ToLowerInvariant()}.", StringComparison.OrdinalIgnoreCase))
            .OrderBy(p => p);
    }

    /// <summary>
    /// Gets all available categories from PermissionCategory
    /// </summary>
    /// <returns>Collection of all available categories</returns>
    public static IEnumerable<PermissionCategory> GetAllCategories()
    {
        return PermissionCategory.GetAll();
    }

    /// <summary>
    /// Gets only system-defined categories
    /// </summary>
    /// <returns>Collection of system categories</returns>
    public static IEnumerable<PermissionCategory> GetSystemCategories()
    {
        return PermissionCategory.GetAll(); // All categories in current implementation are system-defined
    }

    /// <summary>
    /// Gets the permission category from a permission name
    /// If the category code is not found, returns the custom category
    /// </summary>
    /// <param name="permission">The name of the permission</param>
    /// <returns>The permission category</returns>
    public static PermissionCategory GetPermissionCategory(string permission)
    {
        var categoryCode = permission.Split('.')[0];
        if (string.IsNullOrEmpty(categoryCode))
        {
            return PermissionCategory.Custom;
        }

        return PermissionCategory.FromCode(categoryCode);
    }
}