using Ardalis.GuardClauses;
using Endatix.Core.Abstractions;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;

namespace Endatix.Core.UseCases.TenantSettings.Get;

/// <summary>
/// Handler for retrieving tenant settings for the current tenant.
/// Sensitive data (tokens, API keys, URLs) are masked for security.
/// </summary>
public class GetTenantSettingsHandler(
    IRepository<Entities.TenantSettings> repository,
    ITenantContext tenantContext) : IQueryHandler<GetTenantSettingsQuery, Result<TenantSettingsDto>>
{
    public async Task<Result<TenantSettingsDto>> Handle(GetTenantSettingsQuery request, CancellationToken cancellationToken)
    {
        Guard.Against.NegativeOrZero(tenantContext.TenantId);

        var spec = new TenantSettingsByTenantIdSpec(tenantContext.TenantId);
        var tenantSettings = await repository.FirstOrDefaultAsync(spec, cancellationToken);

        if (tenantSettings == null)
        {
            return Result.NotFound("Tenant settings not found.");
        }

        var dto = MapToDto(tenantSettings);
        return Result.Success(dto);
    }

    /// <summary>
    /// Maps TenantSettings entity to DTO with sensitive data masked.
    /// </summary>
    private static TenantSettingsDto MapToDto(Entities.TenantSettings tenantSettings)
    {
        return new TenantSettingsDto
        {
            TenantId = tenantSettings.TenantId,
            SubmissionTokenExpiryHours = tenantSettings.SubmissionTokenExpiryHours,
            IsSubmissionTokenValidAfterCompletion = tenantSettings.IsSubmissionTokenValidAfterCompletion,
            SlackSettings = MapSlackSettings(tenantSettings.SlackSettings),
            WebHookSettings = MapWebHookConfiguration(tenantSettings.WebHookSettings),
            CustomExports = MapCustomExports(tenantSettings.CustomExports),
            ModifiedAt = tenantSettings.ModifiedAt
        };
    }

    /// <summary>
    /// Maps Slack settings with Token field masked.
    /// </summary>
    private static SlackSettingsDto? MapSlackSettings(SlackSettings? slackSettings)
    {
        if (slackSettings == null)
        {
            return null;
        }

        return new SlackSettingsDto
        {
            Token = MaskValue(slackSettings.Token),
            EndatixHubBaseUrl = slackSettings.EndatixHubBaseUrl,
            ChannelId = slackSettings.ChannelId,
            Active = slackSettings.Active
        };
    }

    /// <summary>
    /// Masks a value by replacing every character with *.
    /// </summary>
    private static string? MaskValue(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        return new string('*', value.Length);
    }

    /// <summary>
    /// Maps webhook configuration with sensitive data masked.
    /// </summary>
    private static WebHookConfigurationDto? MapWebHookConfiguration(WebHookConfiguration? webHookConfig)
    {
        if (webHookConfig == null || !webHookConfig.Events.Any())
        {
            return null;
        }

        var eventsDto = new Dictionary<string, WebHookEventConfigDto>();

        foreach (var (eventName, eventConfig) in webHookConfig.Events)
        {
            eventsDto[eventName] = new WebHookEventConfigDto
            {
                IsEnabled = eventConfig.IsEnabled,
                WebHookEndpoints = eventConfig.WebHookEndpoints
                    .Select(MapWebHookEndpoint)
                    .ToList()
            };
        }

        return new WebHookConfigurationDto
        {
            Events = eventsDto
        };
    }

    /// <summary>
    /// Maps webhook endpoint.
    /// </summary>
    private static WebHookEndpointConfigDto MapWebHookEndpoint(WebHookEndpointConfig endpoint)
    {
        return new WebHookEndpointConfigDto
        {
            Url = endpoint.Url
        };
    }

    /// <summary>
    /// Maps custom export configurations.
    /// </summary>
    private static List<CustomExportConfigurationDto>? MapCustomExports(List<CustomExportConfiguration>? customExports)
    {
        if (customExports == null || !customExports.Any())
        {
            return null;
        }

        return customExports.Select(export => new CustomExportConfigurationDto
        {
            Id = export.Id,
            Name = export.Name,
            SqlFunctionName = export.SqlFunctionName
        }).ToList();
    }
}
