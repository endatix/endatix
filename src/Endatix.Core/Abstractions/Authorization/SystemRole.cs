using Ardalis.GuardClauses;

namespace Endatix.Core.Abstractions.Authorization;


/// <summary>
/// Represents a system role with its name, description, and permissions. Useful for data seeding and role management.
/// System roles are predefined roles that are used by the system and cannot be deleted.
/// </summary>
public sealed record SystemRole
{
    private SystemRole(string name, string description, bool isSystemDefined, bool hasHubAccess, string[] permissions, bool isPersisted = true)
    {
        Guard.Against.NullOrWhiteSpace(name, nameof(name));
        Guard.Against.NullOrWhiteSpace(description, nameof(description));
        Guard.Against.Null(permissions, nameof(permissions));

        Name = name;
        Description = description;
        IsSystemDefined = isSystemDefined;
        HasHubAccess = hasHubAccess;
        Permissions = permissions;
        IsPersisted = isPersisted;
    }

    public string Name { get; }
    public string Description { get; }
    public bool IsSystemDefined { get; }
    public bool IsPersisted { get; }
    public bool HasHubAccess { get; }
    public string[] Permissions { get; }

    public static readonly SystemRole Admin = new(
        name: "Admin",
        description: "System administrator with full access to all features and settings of their tenant.",
        isSystemDefined: true,
        hasHubAccess: true,
        permissions: [
            Actions.Access.Hub
        ]);

    public static readonly SystemRole Creator = new(
        name: "Creator",
        description: "Form designer who can create, edit, and manage forms and templates.",
        isSystemDefined: true,
        hasHubAccess: true,
        permissions: [
            Actions.Access.Hub,
            Actions.Forms.View,
            Actions.Forms.Create,
            Actions.Forms.Edit,
             Actions.Questions.View,
            Actions.Questions.Create,
            Actions.Templates.View,
            Actions.Templates.Create,
            Actions.Templates.Edit,
            Actions.Themes.View,
            Actions.Themes.Create,
            Actions.Themes.Edit,
            Actions.Themes.Delete,
            Actions.Submissions.View,
            Actions.Submissions.Create,
            Actions.Submissions.Edit,
            Actions.Submissions.Export,
            Actions.Submissions.DeleteOwned,
        ]);

    public static readonly SystemRole Authenticated = new(
        name: "Authenticated",
        description: "Read-only user who can view forms, submissions, and basic analytics.",
        isSystemDefined: true,
        hasHubAccess: true,
        permissions: [Actions.Access.Authenticated],
        isPersisted: false);

    public static readonly SystemRole Public = new(
        name: "Public",
        description: "Anonymous user who can only access public forms and submit responses.",
        isSystemDefined: true,
        hasHubAccess: true,
        permissions: [],
        isPersisted: false);

    public static readonly SystemRole PlatformAdmin = new(
        name: "PlatformAdmin",
        description: "Platform administrator with full access to all features and settings of the platform.",
        isSystemDefined: true,
        hasHubAccess: true,
        permissions: [],
        isPersisted: true);

    public static readonly SystemRole[] AllSystemRoles = [
        Admin,
        Creator,
        Authenticated,
        Public,
        PlatformAdmin
        ];

    public static readonly string[] AllSystemRoleNames = AllSystemRoles
        .Select(role => role.Name)
        .ToArray();
}