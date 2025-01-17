using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Ardalis.GuardClauses;
using Endatix.Core.Entities;
using Endatix.Core.Events;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Integrations.Slack;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Endatix.Infrastructure.Integrations.Slack;
/// <summary>
/// A client for communications with the Slack API.
/// </summary>
public class SlackClient : INotificationHandler<SubmissionCompletedEvent>, ISlackClient
{
    private readonly ILogger<SlackClient> _logger;
    private readonly SlackSettings _slackSettings;
    private readonly IServiceProvider _serviceProvider;
    private readonly IHttpClientFactory _httpClientFactory;
    //TODO: The Slack message template should be configurable through the Hub's UI
    // {0} is the submissionURL, {1} is the form name
    private const string SLACK_MESSAGE_TEMPLATE = ":page_with_curl: <{0}|New submission> for {1}";
    // Submission URL parameters: {0} is the Hub's base URL, {1} is FormId, {2} is SubmissionId
    private const string SLACK_SUBMISSIONS_URL_TEMPLATE = "{0}/forms/{1}/submissions/{2}";
    private const string SLACK_API_POST_URL = "https://slack.com/api/chat.postMessage";
    public SlackClient(ILogger<SlackClient> logger,
            IOptions<SlackSettings> options,
            IServiceProvider serviceProvider,
            IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _slackSettings = options.Value;
        _serviceProvider = serviceProvider;
        _httpClientFactory = httpClientFactory;

        Guard.Against.Null(_slackSettings.Active, nameof(_slackSettings.Active), "Slack settings must specify whether the integration is active.");
        if (_slackSettings.Active.Value)
        {
            Guard.Against.NullOrEmpty(_slackSettings.EndatixHubBaseUrl, nameof(_slackSettings.EndatixHubBaseUrl), "EndatixHubBaseUrl must have a value within the Slack settings collection.");
            Guard.Against.NullOrEmpty(_slackSettings.Token, nameof(_slackSettings.Token), "Token must have a value within the Slack settings collection.");
            Guard.Against.NullOrEmpty(_slackSettings.ChannelId, nameof(_slackSettings.ChannelId), "ChannelId must have a value within the Slack settings collection.");
        }
    }

    public async Task Handle(SubmissionCompletedEvent notification, CancellationToken cancellationToken)
    {
        if (!_slackSettings.Active!.Value)
        {
            _logger.LogInformation($"Slack notifications are not active.");
            return;
        }

        if (notification.Submission.IsComplete)
        {
            var formNameOrId = notification.Submission.FormId.ToString();
            var submissionUrl = string.Format(SLACK_SUBMISSIONS_URL_TEMPLATE, _slackSettings.EndatixHubBaseUrl!.TrimEnd('\\', '/'), notification.Submission.FormId, notification.Submission.Id);
            Form? form;

            using (var scope = _serviceProvider.CreateScope())
            {
                var repository = scope.ServiceProvider.GetRequiredService<IRepository<Form>>();
                form = await repository.GetByIdAsync(notification.Submission.FormId, cancellationToken);
            }

            // TODO: Remove when per-account settings are implemented
            if(form != null && form.CreatedAt.CompareTo(new DateTime(2021,1,1)) < 0) {
                _logger.LogInformation($"Functional test form - skip Slack notification.");
                return;
            }

            if(form != null && !string.IsNullOrEmpty(form.Name)) {
                formNameOrId = form.Name;
            }
            else {
                _logger.LogWarning($"Form with id {formNameOrId} cannot be loaded by the Slack client");
            }

            var message = string.Format(SLACK_MESSAGE_TEMPLATE, submissionUrl, formNameOrId);

            await PostMessageAsync(message);
        }
        else
        {
            _logger.LogWarning($"SubmissionCompletedEvent raised on an incomplete submission with id {notification.Submission.Id}");
        }
    }

    private async Task PostMessageAsync(string message)
    {
        Guard.Against.NullOrEmpty(message, nameof(message), "ChannelId must have a value within the Slack settings collection.");
        
        var httpClient = _httpClientFactory.CreateClient();

        httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_slackSettings.Token}");

        var payload = new
        {
            channel = _slackSettings.ChannelId,
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
            _logger.LogInformation($"Slack notification posted to channel {_slackSettings.ChannelId}: {message}");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to post a message to Slack. Excption: {ex.Message}");
        }
    }
}