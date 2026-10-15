using System.Text;
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
/// <remarks>
/// Slack notifications are best effort: the submission is already saved when this handler runs,
/// so a missing or incomplete Slack configuration, or a failing Slack API, is logged and never thrown.
/// </remarks>
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
    // Slack runs inside the submission request, so a slow Slack API must not hold the response for long
    private static readonly TimeSpan SlackApiTimeout = TimeSpan.FromSeconds(10);

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
        try
        {
            await NotifyAsync(notification.Submission, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Slack notification failed for submission {SubmissionId} of tenant {TenantId}.",
                notification.Submission.Id,
                notification.Submission.TenantId);
        }
    }

    private async Task NotifyAsync(Submission submission, CancellationToken cancellationToken)
    {
        if (!submission.IsComplete)
        {
            _logger.LogWarning("SubmissionCompletedEvent raised on an incomplete submission with ID {SubmissionId}", submission.Id);
            return;
        }

        var slackSettings = await GetUsableSlackSettingsAsync(submission.TenantId, cancellationToken);
        if (slackSettings is null)
        {
            return;
        }

        var message = await BuildMessageAsync(submission, slackSettings.EndatixHubBaseUrl, cancellationToken);
        await PostMessageAsync(message, slackSettings, cancellationToken);
    }

    /// <summary>
    /// Returns the tenant's Slack settings when the integration is active and fully configured; otherwise logs why and returns null.
    /// </summary>
    private async Task<SlackSettings?> GetUsableSlackSettingsAsync(long tenantId, CancellationToken cancellationToken)
    {
        var tenantSettings = await _tenantSettingsRepository.FirstOrDefaultAsync(
            new TenantSettingsByTenantIdSpec(tenantId), cancellationToken);
        if (tenantSettings is null)
        {
            _logger.LogDebug("Tenant {TenantId} has no settings row; skipping Slack notification.", tenantId);
            return null;
        }

        var slackSettings = tenantSettings.SlackSettings;
        return IsUsable(slackSettings, tenantId) ? slackSettings : null;
    }

    private bool IsUsable(SlackSettings slackSettings, long tenantId)
    {
        if (slackSettings.Active != true)
        {
            _logger.LogDebug("Slack notifications are not active for tenant {TenantId}", tenantId);
            return false;
        }

        if (string.IsNullOrEmpty(slackSettings.EndatixHubBaseUrl)
            || string.IsNullOrEmpty(slackSettings.Token)
            || string.IsNullOrEmpty(slackSettings.ChannelId))
        {
            _logger.LogWarning(
                "Slack is active for tenant {TenantId} but EndatixHubBaseUrl, Token or ChannelId is missing; skipping Slack notification.",
                tenantId);
            return false;
        }

        return true;
    }

    private async Task<string> BuildMessageAsync(Submission submission, string hubBaseUrl, CancellationToken cancellationToken)
    {
        var submissionUrl = string.Format(SLACK_SUBMISSIONS_URL_TEMPLATE, hubBaseUrl.TrimEnd('\\', '/'), submission.FormId, submission.Id);
        var form = await _formRepository.GetByIdAsync(submission.FormId, cancellationToken);
        var formNameOrId = form?.Name;
        if (string.IsNullOrEmpty(formNameOrId))
        {
            formNameOrId = submission.FormId.ToString();
            _logger.LogWarning("Form with ID {FormId} cannot be loaded by the Slack client", formNameOrId);
        }

        return string.Format(SLACK_MESSAGE_TEMPLATE, submissionUrl, formNameOrId);
    }

    private async Task PostMessageAsync(string message, SlackSettings slackSettings, CancellationToken cancellationToken)
    {
        var httpClient = _httpClientFactory.CreateClient();
        httpClient.Timeout = SlackApiTimeout;
        httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {slackSettings.Token}");

        var payload = new
        {
            channel = slackSettings.ChannelId,
            text = message
        };

        using var content = new StringContent(
            System.Text.Json.JsonSerializer.Serialize(payload),
            Encoding.UTF8,
            "application/json"
        );

        var response = await httpClient.PostAsync(SLACK_API_POST_URL, content, cancellationToken);
        response.EnsureSuccessStatusCode();
        _logger.LogInformation("Slack notification posted to channel {ChannelId}: {Message}", slackSettings.ChannelId, message);
    }
}
