using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using Ardalis.GuardClauses;
using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Infrastructure.Domain;

namespace Endatix.Core.Entities;

/// <summary>
/// Represents tenant-specific configuration settings.
/// </summary>
public sealed class TenantSettings : IAggregateRoot, ITenantOwned
{
    public const string DefaultRegistrationRole = "Respondent";

    private string? _slackSettingsJson;
    private SlackSettings? _slackSettings;
    private string? _webHookSettingsJson;
    private WebHookConfiguration? _webHookSettings;
    private string? _customExportsJson;
    private List<CustomExportConfiguration>? _customExports;
    private string? _allowedAuthProviderKeysJson;
    private List<string>? _allowedAuthProviderKeys;

    private TenantSettings() { } // For EF Core

    public TenantSettings(long tenantId, int? submissionTokenExpiryHours = 24, bool isSubmissionTokenValidAfterCompletion = false, string? slackSettingsJson = null, string? webHookSettingsJson = null, string? customExportsJson = null)
    {
        Guard.Against.NegativeOrZero(tenantId, nameof(tenantId));

        TenantId = tenantId;
        SubmissionTokenExpiryHours = submissionTokenExpiryHours;
        IsSubmissionTokenValidAfterCompletion = isSubmissionTokenValidAfterCompletion;
        SlackSettingsJson = slackSettingsJson;
        WebHookSettingsJson = webHookSettingsJson;
        CustomExportsJson = customExportsJson;
        RequireFolderAssignment = false;
        AllowSelfRegistration = false;
        DefaultRegistrationRoleName = DefaultRegistrationRole;
    }

    /// <summary>
    /// Gets the tenant identifier. This serves as the primary key.
    /// </summary>
    public long TenantId { get; private set; }

    /// <summary>
    /// Gets the submission token expiration time in hours.
    /// Null value indicates that tokens never expire.
    /// </summary>
    public int? SubmissionTokenExpiryHours { get; private set; }

    /// <summary>
    /// Gets a value indicating whether submission tokens remain valid after submission completion.
    /// When true, tokens can be used to access completed submissions.
    /// When false (default), tokens become invalid once a submission is marked as complete.
    /// </summary>
    public bool IsSubmissionTokenValidAfterCompletion { get; private set; }

    /// <summary>
    /// When true, forms and templates must be assigned to a folder on create/update.
    /// </summary>
    public bool RequireFolderAssignment { get; private set; }

    /// <summary>
    /// When true, anonymous users may self-register via the tenant slug URL.
    /// </summary>
    public bool AllowSelfRegistration { get; private set; }

    /// <summary>
    /// JSON array of host auth provider keys allowed for self-registration. Empty means none.
    /// </summary>
    public string? AllowedAuthProviderKeysJson
    {
        get => _allowedAuthProviderKeysJson;
        private set
        {
            _allowedAuthProviderKeysJson = value;
            _allowedAuthProviderKeys = null;
        }
    }

    [NotMapped]
    public IReadOnlyList<string> AllowedAuthProviderKeys =>
        _allowedAuthProviderKeys ??= DeserializeAllowedAuthProviderKeys();

    /// <summary>
    /// Name of the role assigned on self-registration. Default <see cref="DefaultRegistrationRole"/>.
    /// <para>
    /// Held by name, not by foreign key, on purpose. Roles live in <c>AppIdentityDbContext</c> under
    /// the <c>identity</c> schema with its own migration history, so EF cannot model a relationship
    /// to them from here. More importantly, a role name does not identify one row: it resolves as
    /// <c>(name, TenantId)</c> falling back to the global system role (<c>TenantId &lt;= 0</c>), and a
    /// tenant-scoped copy can appear later. A key pinned at configuration time would keep pointing
    /// at the row that was current then, while every other lookup moved to the tenant's own copy.
    /// </para>
    /// <para>
    /// Consequence: this name is resolved late and is not guaranteed to match an existing role.
    /// <see cref="IsAllowedDefaultRegistrationRole"/> only rejects roles that must never be used;
    /// callers that persist a policy must check the name actually resolves for the tenant.
    /// </para>
    /// </summary>
    public string DefaultRegistrationRoleName { get; private set; } = DefaultRegistrationRole;

    public string? SlackSettingsJson
    {
        get => _slackSettingsJson;
        private set
        {
            _slackSettingsJson = value;
            _slackSettings = null; // Clear cached settings
        }
    }

    [NotMapped]
    public SlackSettings SlackSettings
    {
        get => _slackSettings ??= DeserializeSlackSettings();
    }

    public string? WebHookSettingsJson
    {
        get => _webHookSettingsJson;
        private set
        {
            _webHookSettingsJson = value;
            _webHookSettings = null; // Clear cached settings
        }
    }

    [NotMapped]
    public WebHookConfiguration WebHookSettings
    {
        get => _webHookSettings ??= DeserializeWebHookSettings();
    }

    public string? CustomExportsJson
    {
        get => _customExportsJson;
        private set
        {
            _customExportsJson = value;
            _customExports = null; // Clear cached settings
        }
    }

    [NotMapped]
    public List<CustomExportConfiguration> CustomExports
    {
        get => _customExports ??= DeserializeCustomExports();
    }

    /// <summary>
    /// Gets the date and time when these settings were last modified.
    /// Useful for tracking configuration changes.
    /// </summary>
    public DateTime? ModifiedAt { get; private set; }

    // Navigation property
    public Tenant Tenant { get; private set; } = null!;

    /// <summary>
    /// Updates the submission token expiration time in hours.
    /// </summary>
    /// <param name="hours">The number of hours until expiration, or null for no expiration.</param>
    public void UpdateSubmissionTokenExpiry(int? hours)
    {
        if (hours.HasValue)
        {
            Guard.Against.NegativeOrZero(hours.Value, nameof(hours));
        }

        SubmissionTokenExpiryHours = hours;
        ModifiedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Updates whether submission tokens remain valid after completion.
    /// </summary>
    /// <param name="isValid">True to allow token access after completion, false otherwise.</param>
    public void UpdateSubmissionTokenValidAfterCompletion(bool isValid)
    {
        IsSubmissionTokenValidAfterCompletion = isValid;
    }

    /// <summary>
    /// Updates the Slack integration settings.
    /// </summary>
    public void UpdateSlackSettings(SlackSettings settings)
    {
        _slackSettings = settings;
        SlackSettingsJson = JsonSerializer.Serialize(settings);
    }

    /// <summary>
    /// Updates the webhook configuration settings.
    /// </summary>
    public void UpdateWebHookSettings(WebHookConfiguration settings)
    {
        _webHookSettings = settings;
        WebHookSettingsJson = JsonSerializer.Serialize(settings);
    }

    /// <summary>
    /// Updates the custom export configurations.
    /// </summary>
    public void UpdateCustomExports(List<CustomExportConfiguration> exports)
    {
        _customExports = exports;
        CustomExportsJson = JsonSerializer.Serialize(exports);
    }

    /// <summary>
    /// Updates whether folder assignment is required for forms and templates.
    /// </summary>
    public void UpdateRequireFolderAssignment(bool require)
    {
        RequireFolderAssignment = require;
        ModifiedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Updates self-registration policy. Rejects forbidden default roles (PlatformAdmin, Public,
    /// non-persisted). Does not verify that <paramref name="defaultRegistrationRoleName"/> exists -
    /// see <see cref="DefaultRegistrationRoleName"/> for why, and validate at the write boundary.
    /// </summary>
    public void UpdateSelfRegistrationPolicy(
        bool allowSelfRegistration,
        IReadOnlyList<string>? allowedAuthProviderKeys,
        string defaultRegistrationRoleName)
    {
        Guard.Against.NullOrWhiteSpace(defaultRegistrationRoleName, nameof(defaultRegistrationRoleName));
        EnsureAllowedDefaultRegistrationRole(defaultRegistrationRoleName);

        AllowSelfRegistration = allowSelfRegistration;
        DefaultRegistrationRoleName = defaultRegistrationRoleName.Trim();
        var keys = (allowedAuthProviderKeys ?? [])
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .Select(key => key.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        // Assign the JSON first: its setter clears the cache, so seed the cache afterwards.
        AllowedAuthProviderKeysJson = keys.Count == 0 ? null : JsonSerializer.Serialize(keys);
        _allowedAuthProviderKeys = keys;
        ModifiedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Returns true when the role name is not one that must never be a self-registration default
    /// (<c>PlatformAdmin</c>, <c>Public</c>, or a non-persisted system role).
    /// <para>
    /// This is a policy check, not an existence check: an unknown name passes. The domain cannot
    /// reach the identity store, so whoever persists the policy must confirm the name resolves for
    /// the tenant, or self-registration will fail at role-assignment time.
    /// </para>
    /// </summary>
    public static bool IsAllowedDefaultRegistrationRole(string roleName)
    {
        if (string.IsNullOrWhiteSpace(roleName))
        {
            return false;
        }

        if (SystemRole.IsPlatformAdminRoleName(roleName) ||
            string.Equals(roleName, SystemRole.Public.Name, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var systemRole = SystemRole.AllSystemRoles
            .FirstOrDefault(role => string.Equals(role.Name, roleName, StringComparison.OrdinalIgnoreCase));
        if (systemRole is not null && !systemRole.IsPersisted)
        {
            return false;
        }

        return true;
    }

    private static void EnsureAllowedDefaultRegistrationRole(string roleName)
    {
        if (!IsAllowedDefaultRegistrationRole(roleName))
        {
            throw new ArgumentException(
                $"Default registration role '{roleName}' is not allowed. Use a persisted tenant role (default: {DefaultRegistrationRole}).",
                nameof(roleName));
        }
    }

    private List<string> DeserializeAllowedAuthProviderKeys()
    {
        if (string.IsNullOrEmpty(AllowedAuthProviderKeysJson))
        {
            return [];
        }

        return JsonSerializer.Deserialize<List<string>>(AllowedAuthProviderKeysJson) ?? [];
    }

    private SlackSettings DeserializeSlackSettings()
    {
        if (string.IsNullOrEmpty(SlackSettingsJson))
        {
            return new SlackSettings { Active = false };
        }

        return JsonSerializer.Deserialize<SlackSettings>(SlackSettingsJson) ??
               new SlackSettings { Active = false };
    }

    private WebHookConfiguration DeserializeWebHookSettings()
    {
        if (string.IsNullOrEmpty(WebHookSettingsJson))
        {
            return new WebHookConfiguration();
        }

        return JsonSerializer.Deserialize<WebHookConfiguration>(WebHookSettingsJson) ??
               new WebHookConfiguration();
    }

    private List<CustomExportConfiguration> DeserializeCustomExports()
    {
        if (string.IsNullOrEmpty(CustomExportsJson))
        {
            return new List<CustomExportConfiguration>();
        }

        return JsonSerializer.Deserialize<List<CustomExportConfiguration>>(CustomExportsJson) ??
               new List<CustomExportConfiguration>();
    }
}
