using Endatix.Core.UseCases.TenantSettings;

namespace Endatix.Api.Endpoints.TenantSettings;

/// <summary>
/// Mapper from TenantSettingsDto to TenantSettingsModel.
/// </summary>
public static class TenantSettingsMapper
{
    /// <summary>
    /// Maps TenantSettingsDto to TenantSettingsModel.
    /// </summary>
    public static TenantSettingsModel Map(TenantSettingsDto dto)
    {
        return new TenantSettingsModel
        {
            TenantId = dto.TenantId,
            SubmissionTokenExpiryHours = dto.SubmissionTokenExpiryHours,
            IsSubmissionTokenValidAfterCompletion = dto.IsSubmissionTokenValidAfterCompletion,
            SlackSettings = dto.SlackSettings != null ? MapSlackSettings(dto.SlackSettings) : null,
            WebHookSettings = dto.WebHookSettings != null ? MapWebHookConfiguration(dto.WebHookSettings) : null,
            CustomExports = dto.CustomExports != null ? MapCustomExports(dto.CustomExports) : null,
            ModifiedAt = dto.ModifiedAt
        };
    }

    private static SlackSettingsModel MapSlackSettings(SlackSettingsDto dto)
    {
        return new SlackSettingsModel
        {
            Token = dto.Token,
            EndatixHubBaseUrl = dto.EndatixHubBaseUrl,
            ChannelId = dto.ChannelId,
            Active = dto.Active
        };
    }

    private static WebHookConfigurationModel MapWebHookConfiguration(WebHookConfigurationDto dto)
    {
        var events = new Dictionary<string, WebHookEventConfigModel>();

        foreach (var (eventName, eventConfig) in dto.Events)
        {
            events[eventName] = new WebHookEventConfigModel
            {
                IsEnabled = eventConfig.IsEnabled,
                WebHookEndpoints = eventConfig.WebHookEndpoints
                    .Select(MapWebHookEndpoint)
                    .ToList()
            };
        }

        return new WebHookConfigurationModel
        {
            Events = events
        };
    }

    private static WebHookEndpointConfigModel MapWebHookEndpoint(WebHookEndpointConfigDto dto)
    {
        return new WebHookEndpointConfigModel
        {
            Url = dto.Url
        };
    }

    private static List<CustomExportConfigurationModel> MapCustomExports(List<CustomExportConfigurationDto> dtos)
    {
        return dtos.Select(dto => new CustomExportConfigurationModel
        {
            Id = dto.Id.ToString(),
            Name = dto.Name,
            SqlFunctionName = dto.SqlFunctionName
        }).ToList();
    }
}
