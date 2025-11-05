using Endatix.Core.Entities.Identity;

namespace Endatix.Infrastructure.Identity.Authorization;


/// <summary>
/// Access-level permissions (not action based permissions)
/// </summary>
public static class Access
{
    /// <summary>
    /// User is authenticated (signed in). Basic authentication level authorization.
    /// </summary>
    public const string Authenticated = "access.authenticated";

    public static class Apps
    {
        public const string Hub = "access.apps.hub";
    }
}

/// <summary>
/// Platform-level permissions for cross-tenant operations (Phase 2).
/// </summary>
public static class Platform
{
    /// <summary>
    /// Platform administrator - can access all tenants and manage platform settings.
    /// </summary>
    public const string Admin = "platform.admin";

    /// <summary>
    /// View platform-level metrics and analytics across all tenants.
    /// </summary>
    public const string ViewMetrics = "platform.metrics.view";
}

/// <summary>
/// Defines basic set of tenant level actions in the Endatix system to define the permission system.
/// Action names follow the pattern: {category}.{action}[.scope]
/// </summary>
public static class Actions
{
    /// <summary>
    /// Administrative permissions - bypass all other checks (system-wide)
    /// </summary>
    public static class Admin
    {
        public const string All = "admin.all";
        public const string ManageUsers = "admin.users.manage";
        public const string ManageRoles = "admin.roles.manage";
        public const string ViewSystemLogs = "admin.logs.view";
        public const string ManageSystemSettings = "admin.settings.manage";
    }

    /// <summary>
    /// Form management permissions (Scripter role)
    /// </summary>
    public static class Forms
    {
        public const string View = "forms.view";
        public const string Create = "forms.create";
        public const string Edit = "forms.edit";
        public const string Delete = "forms.delete";
        public const string Publish = "forms.publish";

        // Ownership-based permissions
        public const string ViewOwned = "forms.view.owned";
        public const string EditOwned = "forms.edit.owned";
        public const string DeleteOwned = "forms.delete.owned";
        public const string PublishOwned = "forms.publish.owned";
    }

    /// <summary>
    /// Submission management permissions (Panelist role)
    /// </summary>
    public static class Submissions
    {
        public const string View = "submissions.view";
        public const string Create = "submissions.create";
        public const string Edit = "submissions.edit";
        public const string Delete = "submissions.delete";
        public const string Export = "submissions.export";

        // Public submission permissions (for anonymous/public users)
        public const string Submit = "submissions.submit";
        public const string UpdatePartial = "submissions.update.partial";
        public const string ViewPublicForm = "submissions.view.public.form";

        // Ownership-based permissions
        public const string ViewOwned = "submissions.view.owned";
        public const string EditOwned = "submissions.edit.owned";
        public const string DeleteOwned = "submissions.delete.owned";
        public const string ExportOwned = "submissions.export.owned";
    }

    /// <summary>
    /// Template management permissions
    /// </summary>
    public static class Templates
    {
        public const string View = "templates.view";
        public const string Create = "templates.create";
        public const string Edit = "templates.edit";
        public const string Delete = "templates.delete";

        // Ownership-based permissions
        public const string ViewOwned = "templates.view.owned";
        public const string EditOwned = "templates.edit.owned";
        public const string DeleteOwned = "templates.delete.owned";
    }

    /// <summary>
    /// Theme management permissions
    /// </summary>
    public static class Themes
    {
        public const string View = "themes.view";
        public const string Create = "themes.create";
        public const string Edit = "themes.edit";
        public const string Delete = "themes.delete";

        // Ownership-based permissions
        public const string ViewOwned = "themes.view.owned";
        public const string EditOwned = "themes.edit.owned";
        public const string DeleteOwned = "themes.delete.owned";
    }

    /// <summary>
    /// Question management permissions
    /// </summary>
    public static class Questions
    {
        public const string View = "questions.view";
        public const string Create = "questions.create";
        public const string Edit = "questions.edit";
        public const string Delete = "questions.delete";

        // Ownership-based permissions
        public const string ViewOwned = "questions.view.owned";
        public const string EditOwned = "questions.edit.owned";
        public const string DeleteOwned = "questions.delete.owned";
    }

    /// <summary>
    /// Analytics and reporting permissions
    /// </summary>
    public static class Analytics
    {
        public const string View = "analytics.view";
        public const string ViewAdvanced = "analytics.view.advanced";
        public const string Export = "analytics.export";
        public const string ViewRealtime = "analytics.realtime.view";

        // Ownership-based permissions
        public const string ViewOwned = "analytics.view.owned";
        public const string ExportOwned = "analytics.export.owned";
    }

    /// <summary>
    /// Tenant management permissions (SaaS management)
    /// </summary>
    public static class Tenant
    {
        public const string View = "tenant.view";
        public const string Manage = "tenant.manage";
        public const string InviteUsers = "tenant.users.invite";
        public const string ManageUsers = "tenant.users.manage";
        public const string ViewBilling = "tenant.billing.view";
        public const string ManageBilling = "tenant.billing.manage";
        public const string ViewSettings = "tenant.settings.view";
        public const string ManageSettings = "tenant.settings.manage";
        public const string ViewUsage = "tenant.usage.view";
        public const string ManageIntegrations = "tenant.integrations.manage";
    }

    /// <summary>
    /// System permissions (infrastructure, health checks, etc.)
    /// </summary>
    public static class System
    {
        public const string HealthCheck = "system.health.check";
        public const string Maintenance = "system.maintenance";
        public const string ViewMetrics = "system.metrics.view";
        public const string ManageIntegrations = "system.integrations.manage";
        public const string ViewLogs = "system.logs.view";
    }

    /// <summary>
    /// Common action types used across different categories
    /// </summary>
    public static class ActionTypes
    {
        public const string View = "view";
        public const string Create = "create";
        public const string Edit = "edit";
        public const string Delete = "delete";
        public const string Publish = "publish";
        public const string Export = "export";
        public const string Manage = "manage";
        public const string Submit = "submit";
        public const string All = "all";
        public const string Invite = "invite";
    }

    /// <summary>
    /// Common scopes used for ownership-based permissions
    /// </summary>
    public static class Scopes
    {
        public const string Owned = "owned";
        public const string Public = "public";
        public const string Partial = "partial";
        public const string Advanced = "advanced";
        public const string Realtime = "realtime";
    }

    /// <summary>
    /// Helper methods for building permission strings dynamically
    /// </summary>
    public static class Builder
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
}