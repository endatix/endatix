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
    private readonly IRepository<Form> _formRepository;
    private readonly SlackSettings _slackSettings;
    private readonly IServiceProvider _serviceProvider;
    private readonly IHttpClientFactory _httpClientFactory;
    //TODO: The Slack message template should be configurable through the Hub's UI
    // {0} is the submissionURL, {1} is the form name
    private const string SLACK_MESSAGE_TEMPLATE = ":page_with_curl: <{0}|New submission> for {1}";
    // Submission URL parameters: {0} is the Hub's base URL, {1} is FormId
    private const string SLACK_SUBMISSIONS_URL_TEMPLATE = "{0}/forms/submissions/{1}";
    public SlackClient(ILogger<SlackClient> logger,
            IRepository<Form> formRepository,
            IOptions<SlackSettings> options,
            IServiceProvider serviceProvider,
            IHttpClientFactory httpClientFactory)
    {
        _formRepository = formRepository;
        _logger = logger;
        _slackSettings = options.Value;
        _serviceProvider = serviceProvider;
        _httpClientFactory = httpClientFactory;
    }

    public async Task Handle(SubmissionCompletedEvent notification, CancellationToken cancellationToken)
    {
        Guard.Against.Null(_slackSettings.Active, nameof(_slackSettings.Active), "Slack settings must specify whether the integration is active.");
        Guard.Against.NullOrEmpty(_slackSettings.EndatixHubBaseUrl, nameof(_slackSettings.EndatixHubBaseUrl), "EndatixHubBaseUrl must have a value within the Slack settings collection.");
        Guard.Against.NullOrEmpty(_slackSettings.Token, nameof(_slackSettings.Token), "Token must have a value within the Slack settings collection.");
        Guard.Against.NullOrEmpty(_slackSettings.ChannelId, nameof(_slackSettings.ChannelId), "ChannelId must have a value within the Slack settings collection.");

        if (notification.Submission.IsComplete && (bool)_slackSettings.Active)
        {

            var submissionUrl = string.Format(SLACK_SUBMISSIONS_URL_TEMPLATE, _slackSettings.EndatixHubBaseUrl.TrimEnd('\\', '/'), notification.Submission.FormId);
            Form? form;

            using (var scope = _serviceProvider.CreateScope())
            {
                var repository = scope.ServiceProvider.GetRequiredService<IRepository<Form>>();
                form = await repository.GetByIdAsync(notification.Submission.FormId, cancellationToken);
            }

            Guard.Against.Null(form, $"Unable to load submission {notification.Submission.Id}'s Form object.");

            var message = string.Format(SLACK_MESSAGE_TEMPLATE, submissionUrl, form.Name);

            await PostMessageAsync(_slackSettings.Token, _slackSettings.ChannelId, message);
        }
        else
        {
            if (!notification.Submission.IsComplete)
            {
                _logger.LogWarning($"SubmissionCompletedEvent raised on an incomplete submission with id {notification.Submission.Id}");
            }

        }
    }

    public async Task PostMessageAsync(string token, string channelId, string message)
    {
        Guard.Against.NullOrEmpty(channelId, nameof(channelId), "EndatixHubBaseUrl must have a value when posting to Slack.");
        Guard.Against.NullOrEmpty(token, nameof(token), "Token must have a value  when posting to Slack.");
        Guard.Against.NullOrEmpty(message, nameof(message), "Message must have a value when posting to Slack.");

        var httpClient = _httpClientFactory.CreateClient();

        httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");

        var payload = new
        {
            channel = channelId,
            text = message
        };

        var content = new StringContent(
            System.Text.Json.JsonSerializer.Serialize(payload),
            Encoding.UTF8,
            "application/json"
        );

        try
        {
            var response = await httpClient.PostAsync("https://slack.com/api/chat.postMessage", content);
            response.EnsureSuccessStatusCode();
            _logger.LogInformation($"Slack notification posted to channel {channelId}: {message}");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to post a message to Slack. Excption: {ex.Message}");
        }
    }
}