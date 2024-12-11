using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Endatix.Core.Entities;
using Endatix.Core.Events;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Integrations.Slack;
using MediatR;
using Microsoft.AspNetCore.Http;
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
    public SlackClient(ILogger<SlackClient> logger, IRepository<Form> formRepository, IOptions<SlackSettings> options) {
        _formRepository = formRepository;
        _logger = logger;
        _slackSettings = options.Value;
    }

    public async Task Handle(SubmissionCompletedEvent notification, CancellationToken cancellationToken) {
        if(notification.Submission.IsComplete && _slackSettings.Active){

            var submissionUrl = $"{_slackSettings.EndatixHubBaseUrl.TrimEnd('\\', '/')}/forms/submissions/{notification.Submission.FormId}";
            
            //TODO: Troubleshoot _formRepository being disposed below and uncomment the line in order to reference the form name correctly
            // var form = await _formRepository.GetByIdAsync(notification.Submission.FormId, cancellationToken) ?? throw new Exception("Form not found by the FormId of the submission");

            var message = $":page_with_curl: <{submissionUrl}|New submission> for form with id {notification.Submission.FormId}";

            await PostMessageAsync(_slackSettings.Token, _slackSettings.ChannelId, message);
        }
    }

    public async Task PostMessageAsync(string token, string channelId, string message)
    {
        using var httpClient = new HttpClient();
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

        try {
            var response = await httpClient.PostAsync("https://slack.com/api/chat.postMessage", content);
            response.EnsureSuccessStatusCode();
            _logger.LogInformation($"Slack notification posted to channel {channelId}: {message}");
        }
        catch(Exception ex) {
            _logger.LogError($"Failed to post a message to Slack. Excption: {ex.Message}");
        }
    }
}