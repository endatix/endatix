using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Ardalis.Specification;
using Endatix.Core.Entities;
using Endatix.Core.Events;
using Endatix.Core.Specifications;
using Endatix.Core.Infrastructure.Domain;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Endatix.Infrastructure.Integrations.Slack;

/// <summary>
/// A client for communications with the Slack API.
/// </summary>
/// <remarks>
/// Slack notifications are best effort: the submission is already saved when this handler runs,
/// so a missing or incomplete Slack configuration, or a failing Slack API, is logged and never thrown.
/// A missing settings row is a warning. Slack turned off is debug, because that is a normal choice.
/// </remarks>
public class SlackNotificationHandler : INotificationHandler<SubmissionCompletedEvent>, ISlackNotificationHandler
{
    public const string HttpClientName = "endatix-slack";

    // Slack runs inside the submission request, so a slow Slack API must not hold the response for long.
    internal static readonly TimeSpan ApiTimeout = TimeSpan.FromSeconds(10);

    private readonly ILogger<SlackNotificationHandler> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IRepository<TenantSettings> _tenantSettingsRepository;
    private readonly IRepository<Form> _formRepository;

    //TODO: The Slack message template should be configurable through the Hub's UI
    // {0} is the submissionURL, {1} is the form name
    private const string MessageTemplate = ":page_with_curl: <{0}|New submission> for {1}";
    // Submission URL parameters: {0} is the Hub's base URL, {1} is FormId, {2} is SubmissionId
    private const string SubmissionsUrlTemplate = "{0}/forms/{1}/submissions/{2}";
    private const string PostUrl = "https://slack.com/api/chat.postMessage";

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

        var target = await GetTargetAsync(submission.TenantId, cancellationToken);
        if (target is null)
        {
            return;
        }

        var message = await BuildMessageAsync(submission, target.HubBaseUrl, cancellationToken);
        await PostMessageAsync(message, target);
    }

    /// <summary>
    /// The tenant's Slack target when the integration is active and fully configured; otherwise logs why and returns null.
    /// </summary>
    private async Task<SlackTarget?> GetTargetAsync(long tenantId, CancellationToken cancellationToken)
    {
        var tenantSettings = await _tenantSettingsRepository.FirstOrDefaultAsync(
            new TenantSettingsByTenantIdSpec(tenantId), cancellationToken);
        if (tenantSettings is null)
        {
            _logger.LogWarning("Tenant {TenantId} has no settings row; skipping Slack notification.", tenantId);
            return null;
        }

        return ToTarget(tenantSettings.SlackSettings, tenantId);
    }

    private SlackTarget? ToTarget(SlackSettings settings, long tenantId)
    {
        if (settings.Active != true)
        {
            _logger.LogDebug("Slack notifications are not active for tenant {TenantId}", tenantId);
            return null;
        }

        var hubBaseUrl = settings.EndatixHubBaseUrl?.Trim();
        var token = settings.Token?.Trim();
        var channelId = settings.ChannelId?.Trim();
        if (string.IsNullOrEmpty(hubBaseUrl) || string.IsNullOrEmpty(token) || string.IsNullOrEmpty(channelId))
        {
            _logger.LogWarning("Slack is active for tenant {TenantId} but a setting is missing; skipping.", tenantId);
            return null;
        }

        return new SlackTarget(hubBaseUrl, token, channelId);
    }

    private async Task<string> BuildMessageAsync(Submission submission, string hubBaseUrl, CancellationToken cancellationToken)
    {
        var submissionUrl = string.Format(SubmissionsUrlTemplate, hubBaseUrl.TrimEnd('\\', '/'), submission.FormId, submission.Id);
        var formName = await FormNameAsync(submission, cancellationToken);
        return string.Format(MessageTemplate, submissionUrl, EscapeMrkdwn(formName));
    }

    private async Task<string> FormNameAsync(Submission submission, CancellationToken cancellationToken)
    {
        var name = await _formRepository.FirstOrDefaultAsync(new FormNameByIdSpec(submission.FormId), cancellationToken);
        if (!string.IsNullOrWhiteSpace(name))
        {
            return name;
        }

        _logger.LogWarning("Form with ID {FormId} cannot be loaded by the Slack client", submission.FormId);
        return submission.FormId.ToString();
    }

    private async Task PostMessageAsync(string message, SlackTarget target)
    {
        using var response = await SendAsync(message, target);
        if (!response.IsSuccessStatusCode)
        {
            LogPostFailed(target.ChannelId, (int)response.StatusCode, "HTTP error");
            return;
        }

        var body = await response.Content.ReadAsStringAsync(CancellationToken.None);
        if (!SlackAccepted(body, out var error))
        {
            LogPostFailed(target.ChannelId, (int)response.StatusCode, error);
            return;
        }

        _logger.LogInformation("Slack notification posted to channel {ChannelId}", target.ChannelId);
    }

    private async Task<HttpResponseMessage> SendAsync(string message, SlackTarget target)
    {
        var httpClient = _httpClientFactory.CreateClient(HttpClientName);
        using var request = new HttpRequestMessage(HttpMethod.Post, PostUrl)
        {
            Content = JsonContent(message, target.ChannelId),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", target.Token);
        return await httpClient.SendAsync(request, CancellationToken.None);
    }

    private void LogPostFailed(string channelId, int statusCode, string error) =>
        _logger.LogError(
            "Slack notification failed for channel {ChannelId}. HTTP {StatusCode}. {Error}",
            channelId,
            statusCode,
            error);

    private static StringContent JsonContent(string message, string channelId) =>
        new(
            JsonSerializer.Serialize(new { channel = channelId, text = message }),
            Encoding.UTF8,
            "application/json");

    // Slack answers most failures with HTTP 200 and {"ok":false,"error":"..."}.
    private static bool SlackAccepted(string body, out string error)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            if (root.TryGetProperty("ok", out var ok) && ok.ValueKind == JsonValueKind.True)
            {
                error = string.Empty;
                return true;
            }
            error = SlackError(root);
            return false;
        }
        catch (JsonException)
        {
            error = "response was not JSON";
            return false;
        }
    }

    private static string SlackError(JsonElement root) =>
        root.TryGetProperty("error", out var slackError) && slackError.ValueKind == JsonValueKind.String
            ? slackError.GetString() ?? "unknown"
            : "ok was not true";

    // Slack mrkdwn treats &, < and > as markup. & is replaced first so the escapes are not escaped again.
    private static string EscapeMrkdwn(string value) =>
        value.Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal);

    private sealed record SlackTarget(string HubBaseUrl, string Token, string ChannelId);
}

/// <summary>The form's name only, without tracking the form.</summary>
internal sealed class FormNameByIdSpec : Specification<Form, string>
{
    public FormNameByIdSpec(long formId)
    {
        Query
            .Where(form => form.Id == formId)
            .AsNoTracking();
        Query.Select(form => form.Name);
    }
}
