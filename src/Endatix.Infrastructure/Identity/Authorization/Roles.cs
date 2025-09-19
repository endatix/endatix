using Endatix.Core.Entities.Identity;

namespace Endatix.Infrastructure.Identity.Authorization;

/// <summary>
/// Defines all built-in roles and their permission sets.
/// Roles are hierarchical: Admin > Scripter > Panelist > Viewer > Public
/// </summary>
public static class Roles
{
    // Role constants
    public const string Admin = "Admin";
    public const string Scripter = "Scripter";  // Form creators and managers
    public const string Panelist = "Panelist"; // Submission managers and data collectors
    public const string Viewer = "Viewer";     // Read-only access to data
    public const string Public = "Public";     // Anonymous/unauthenticated users

    /// <summary>
    /// All defined role names for validation and enumeration
    /// </summary>
    public static readonly string[] AllRoles = [Admin, Scripter, Panelist, Viewer, Public];

    /// <summary>
    /// Role descriptions for UI and documentation
    /// </summary>
    public static readonly Dictionary<string, string> RoleDescriptions = new()
    {
        [Admin] = "System administrator with full access to all features and settings",
        [Scripter] = "Form designer who can create, edit, and manage forms and templates",
        [Panelist] = "Data collector who can manage submissions and view analytics",
        [Viewer] = "Read-only user who can view forms, submissions, and basic analytics",
        [Public] = "Anonymous user who can only access public forms and submit responses"
    };

    /// <summary>
    /// Role hierarchy levels (higher number = more permissions)
    /// </summary>
    public static readonly Dictionary<string, int> RoleHierarchy = new()
    {
        [Public] = 0,
        [Viewer] = 1,
        [Panelist] = 2,
        [Scripter] = 3,
        [Admin] = 4
    };

    /// <summary>
    /// Gets the default permissions for each role.
    /// Permissions are additive based on hierarchy.
    /// </summary>
    public static readonly Dictionary<string, string[]> DefaultPermissions = new()
    {
        [Admin] = [
            // Admin has all permissions via admin.all
            Actions.Admin.All
        ],
        
        [Scripter] = [
            // Full form management (all forms in tenant)
            Actions.Forms.View,
            Actions.Forms.Create,
            Actions.Forms.Edit,
            Actions.Forms.Publish,
            
            // Ownership-based form permissions (for cross-tenant scenarios)
            Actions.Forms.ViewOwned,
            Actions.Forms.EditOwned,
            Actions.Forms.DeleteOwned,
            Actions.Forms.PublishOwned,
            
            // Template management
            Actions.Templates.View,
            Actions.Templates.Create,
            Actions.Templates.Edit,
            Actions.Templates.ViewOwned,
            Actions.Templates.EditOwned,
            Actions.Templates.DeleteOwned,
            
            // View and export submissions for their forms
            Actions.Submissions.View,
            Actions.Submissions.Export,
            Actions.Submissions.ViewOwned,
            Actions.Submissions.ExportOwned,
        ],
        
        [Panelist] = [
            // View forms (read-only)
            Actions.Forms.View,
            Actions.Forms.ViewOwned,
            
            // Submissions
            Actions.Submissions.ViewPublicForm,
            Actions.Submissions.Create,
            Actions.Submissions.ViewOwned,
            Actions.Submissions.EditOwned,
            Actions.Submissions.DeleteOwned
        ],
        
        [Viewer] = [
            // Read-only access to forms
            Actions.Forms.View,
            Actions.Forms.ViewOwned,
            
            // Read-only access to submissions
            Actions.Submissions.View,
            Actions.Submissions.ViewOwned,
            
            // Read-only access to templates
            Actions.Templates.View,
            Actions.Templates.ViewOwned,
            
            // Basic analytics viewing
            Actions.Analytics.View,
            Actions.Analytics.ViewOwned
        ],
        
        [Public] = [
            // Public form access only
            Actions.Submissions.ViewPublicForm,
            Actions.Submissions.Submit,
            Actions.Submissions.UpdatePartial,
            
            // System health check (for load balancers, etc.)
            Actions.System.HealthCheck
        ]
    };

    /// <summary>
    /// Gets permissions for a role including all inherited permissions from lower hierarchy roles
    /// </summary>
    /// <param name="roleName">The role name</param>
    /// <returns>All permissions for the role including inherited ones</returns>
    public static string[] GetAllPermissionsForRole(string roleName)
    {
        if (!DefaultPermissions.ContainsKey(roleName))
        {
            throw new ArgumentException($"Unknown role: {roleName}", nameof(roleName));
        }

        // For Admin role, return admin.all which bypasses all other checks
        if (roleName == Admin)
        {
            return DefaultPermissions[Admin];
        }

        var allPermissions = new HashSet<string>();
        var currentRoleLevel = RoleHierarchy[roleName];

        // Add permissions from all roles at or below current level
        foreach (var role in RoleHierarchy.Where(r => r.Value <= currentRoleLevel))
        {
            if (role.Key != Admin) // Skip admin to avoid adding admin.all to everyone
            {
                foreach (var permission in DefaultPermissions[role.Key])
                {
                    allPermissions.Add(permission);
                }
            }
        }

        // Add current role's specific permissions
        foreach (var permission in DefaultPermissions[roleName])
        {
            allPermissions.Add(permission);
        }

        return allPermissions.OrderBy(p => p).ToArray();
    }

    /// <summary>
    /// Checks if a role exists in the system
    /// </summary>
    /// <param name="roleName">The role name to check</param>
    /// <returns>True if the role exists</returns>
    public static bool IsValidRole(string roleName)
    {
        return AllRoles.Contains(roleName, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Gets the role hierarchy level
    /// </summary>
    /// <param name="roleName">The role name</param>
    /// <returns>The hierarchy level (0 = lowest, higher = more permissions)</returns>
    public static int GetRoleLevel(string roleName)
    {
        return RoleHierarchy.GetValueOrDefault(roleName, -1);
    }

    /// <summary>
    /// Checks if one role has higher or equal permissions than another
    /// </summary>
    /// <param name="roleName">The role to check</param>
    /// <param name="compareToRole">The role to compare against</param>
    /// <returns>True if roleName has higher or equal permissions</returns>
    public static bool HasEqualOrHigherPermissions(string roleName, string compareToRole)
    {
        return GetRoleLevel(roleName) >= GetRoleLevel(compareToRole);
    }

    /// <summary>
    /// Creates role definitions for seeding the database
    /// </summary>
    /// <param name="tenantId">The tenant ID (0 for system-wide roles)</param>
    /// <returns>Collection of role definitions</returns>
    public static IEnumerable<(string Name, string Description, bool IsSystemDefined)> GetRoleDefinitions()
    {
        return AllRoles.Select(role => (
            Name: role,
            Description: RoleDescriptions[role],
            IsSystemDefined: true
        ));
    }
}
