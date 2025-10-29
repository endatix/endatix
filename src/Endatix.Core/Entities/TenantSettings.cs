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

    private TenantSettings() { } // For EF Core

    public TenantSettings(long tenantId, int? submissionTokenExpiryHours = 24, string? slackSettingsJson = null)
    {
        Guard.Against.NegativeOrZero(tenantId, nameof(tenantId));

        TenantId = tenantId;
        SubmissionTokenExpiryHours = submissionTokenExpiryHours;
        SlackSettingsJson = slackSettingsJson;
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
    /// Updates the Slack integration settings.
    /// </summary>
    public void UpdateSlackSettings(SlackSettings settings)
    {
        _slackSettings = settings;
        SlackSettingsJson = JsonSerializer.Serialize(settings);
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
}
