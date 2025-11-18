using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using Ardalis.GuardClauses;
using Endatix.Core.Infrastructure.Domain;

namespace Endatix.Core.Entities;

/// <summary>
/// Represents tenant-specific configuration settings.
/// </summary>
public class TenantSettings : IAggregateRoot
{
    private string? _slackSettingsJson;
    private SlackSettings? _slackSettings;
    private string? _webHookSettingsJson;
    private WebHookConfiguration? _webHookSettings;
    private string? _customExportsJson;
    private List<CustomExportConfiguration>? _customExports;

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
