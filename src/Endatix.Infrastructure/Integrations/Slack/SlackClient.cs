using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Endatix.Core.Integrations.Slack;
using Microsoft.Extensions.Logging;

namespace Endatix.Infrastructure.Integrations.Slack;
/// <summary>
/// A client for communications with the Slack API.
/// </summary>
/// <param name="logger"></param>
public class SlackClient(ILogger<SlackClient> logger) : SlackCLient
{
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

        var response = await httpClient.PostAsync("https://slack.com/api/chat.postMessage", content);
        response.EnsureSuccessStatusCode();
    }
}