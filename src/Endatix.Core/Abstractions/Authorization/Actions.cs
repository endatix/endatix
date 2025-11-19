namespace Endatix.Core.Abstractions.Authorization;

/// <summary>
/// Defines basic set of tenant level actions in the Endatix system to define the permission system.
/// Action names follow the pattern: {category}.{action}[.scope]
/// </summary>
public static class Actions
{


    /// <summary>
    /// Access-level permissions (not action based permissions)
    /// </summary>
    public static class Access
    {
        /// <summary>
        /// User is authenticated (signed in). Basic authentication level authorization.
        /// </summary>
        public const string Authenticated = "access.authenticated";

        /// <summary>
        /// User has access to the Hub application.
        /// </summary>
        public const string Hub = "access.apps.hub";
    }

    /// <summary>
    /// Platform-level permissions
    /// </summary>
    public static class Platform
    {
        public const string ManageTenants = "platform.tenants.manage";
        public const string ManageSettings = "platform.settings.manage";
        public const string ManageIntegrations = "platform.integrations.manage";
        public const string ImpersonateUsers = "platform.users.impersonate";
        public const string ViewMetrics = "platform.metrics.view";
        public const string ViewLogs = "platform.logs.view";
        public const string ViewUsage = "platform.usage.view";
    }

    /// <summary>
    /// Tenant-level permissions
    /// </summary>
    public static class Tenant
    {
        public const string InviteUsers = "tenant.users.invite";

        public const string ViewUsers = "tenant.users.view";

        public const string ManageUsers = "tenant.users.manage";

        public const string ViewRoles = "tenant.roles.view";
        public const string ManageRoles = "tenant.roles.manage";
        public const string ViewSettings = "tenant.settings.view";
        public const string ManageSettings = "tenant.settings.manage";
        public const string ViewUsage = "tenant.usage.view";
    }

    /// <summary>
    /// Form management permissions
    /// </summary>
    public static class Forms
    {
        public const string View = "forms.view";
        public const string Create = "forms.create";
        public const string Edit = "forms.edit";
        public const string Delete = "forms.delete";
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
    }

    /// <summary>
    /// Submission management permissions
    /// </summary>
    public static class Submissions
    {
        public const string View = "submissions.view";
        public const string Create = "submissions.create";
        public const string Edit = "submissions.edit";
        public const string Delete = "submissions.delete";
        public const string Export = "submissions.export";

        // Ownership-based permissions  
        public const string DeleteOwned = "submissions.delete.owned";
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
    }
}