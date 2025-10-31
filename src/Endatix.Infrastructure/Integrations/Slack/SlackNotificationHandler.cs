using System.Text;
using Ardalis.GuardClauses;
using Endatix.Core.Entities;
using Endatix.Core.Events;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Specifications;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Endatix.Infrastructure.Integrations.Slack;
/// <summary>
/// A client for communications with the Slack API.
/// </summary>
public class SlackNotificationHandler : INotificationHandler<SubmissionCompletedEvent>, ISlackNotificationHandler
{
    private readonly ILogger<SlackNotificationHandler> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IRepository<TenantSettings> _tenantSettingsRepository;
    private readonly IRepository<Form> _formRepository;

    //TODO: The Slack message template should be configurable through the Hub's UI
    // {0} is the submissionURL, {1} is the form name
    private const string SLACK_MESSAGE_TEMPLATE = ":page_with_curl: <{0}|New submission> for {1}";
    // Submission URL parameters: {0} is the Hub's base URL, {1} is FormId, {2} is SubmissionId
    private const string SLACK_SUBMISSIONS_URL_TEMPLATE = "{0}/forms/{1}/submissions/{2}";
    private const string SLACK_API_POST_URL = "https://slack.com/api/chat.postMessage";

    public SlackNotificationHandler(
        ILogger<SlackNotificationHandler> logger,
        IHttpClientFactory httpClientFactory,
        IRepository<TenantSettings> tenantSettingsRepository,
        IRepository<Form> formRepository)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _tenantSettingsRepository = tenantSettingsRepository;
        _formRepository = formRepository;
    }

    public async Task Handle(SubmissionCompletedEvent notification, CancellationToken cancellationToken)
    {
        if (!notification.Submission.IsComplete)
        {
            _logger.LogWarning($"SubmissionCompletedEvent raised on an incomplete submission with ID {notification.Submission.Id}");
            return;
        }

        var tenantSettings = await _tenantSettingsRepository.FirstOrDefaultAsync(
            new TenantSettingsByTenantIdSpec(notification.Submission.TenantId), cancellationToken);
        Guard.Against.Null(tenantSettings, "Tenant settings must be configured.");

        var form = await _formRepository.GetByIdAsync(notification.Submission.FormId, cancellationToken);

        var slackSettings = tenantSettings.SlackSettings;
        Guard.Against.Null(slackSettings.Active, nameof(slackSettings.Active), "Slack settings must specify whether the integration is active.");
        if (!slackSettings.Active!.Value)
        {
            _logger.LogInformation($"Slack notifications are not active for tenant with ID {notification.Submission.TenantId}");
            return;
        }

        Guard.Against.NullOrEmpty(slackSettings.EndatixHubBaseUrl, nameof(slackSettings.EndatixHubBaseUrl), "EndatixHubBaseUrl must have a value within the Slack settings collection.");
        Guard.Against.NullOrEmpty(slackSettings.Token, nameof(slackSettings.Token), "Token must have a value within the Slack settings collection.");
        Guard.Against.NullOrEmpty(slackSettings.ChannelId, nameof(slackSettings.ChannelId), "ChannelId must have a value within the Slack settings collection.");

        var submissionUrl = string.Format(SLACK_SUBMISSIONS_URL_TEMPLATE, slackSettings.EndatixHubBaseUrl!.TrimEnd('\\', '/'), notification.Submission.FormId, notification.Submission.Id);
        string formNameOrId;
        if (form != null && !string.IsNullOrEmpty(form.Name))
        {
            formNameOrId = form.Name;
        }
        else
        {
            formNameOrId = notification.Submission.FormId.ToString();
            _logger.LogWarning($"Form with ID {formNameOrId} cannot be loaded by the Slack client");
        }

        var message = string.Format(SLACK_MESSAGE_TEMPLATE, submissionUrl, formNameOrId);

        await PostMessageAsync(message, slackSettings);
    }

    private async Task PostMessageAsync(string message, SlackSettings slackSettings)
    {
        Guard.Against.NullOrEmpty(message, nameof(message), "Message cannot be empty.");

        var httpClient = _httpClientFactory.CreateClient();

        httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {slackSettings.Token}");

        var payload = new
        {
            channel = slackSettings.ChannelId,
            text = message
        };

        var content = new StringContent(
            System.Text.Json.JsonSerializer.Serialize(payload),
            Encoding.UTF8,
            "application/json"
        );

        try
        {
            var response = await httpClient.PostAsync(SLACK_API_POST_URL, content);
            response.EnsureSuccessStatusCode();
            _logger.LogInformation($"Slack notification posted to channel {slackSettings.ChannelId}: {message}");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to post a message to Slack. Exception: {ex.Message}");
        }
    }
}