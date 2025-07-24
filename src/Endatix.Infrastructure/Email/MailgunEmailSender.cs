using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Endatix.Core;
using Endatix.Core.Abstractions;
using Endatix.Core.Features.Email;
using Ardalis.GuardClauses;

namespace Endatix.Infrastructure.Email;

public class MailgunEmailSender : IEmailSender, IHasConfigSection<MailgunSettings>, IPluginInitializer
{
    private readonly IHttpClientFactory _factory;

    private readonly ILogger _logger;

    private readonly MailgunSettings _settings;

    public MailgunEmailSender(IHttpClientFactory factory, ILogger<MailgunEmailSender> logger, IOptions<MailgunSettings> options)
    {
        _factory = factory;
        _logger = logger;
        _settings = options.Value;
    }

    public static Action<IServiceCollection> InitializationDelegate => (services) =>
    {
        services.AddHttpClient();
    };

    public void Install(IServiceCollection services)
    {
        services.AddHttpClient();
    }

    public async Task SendEmailAsync(EmailWithBody email, CancellationToken cancellationToken = default)
    {
        Guard.Against.NullOrWhiteSpace(email.From);
        Guard.Against.NullOrWhiteSpace(email.Subject);
        Guard.Against.NullOrWhiteSpace(email.PlainTextBody);
        Guard.Against.NullOrWhiteSpace(email.HtmlBody);

        var contentValues = new List<KeyValuePair<string, string>>
        {
            new KeyValuePair<string, string>("from", email.From),
            new KeyValuePair<string, string>("subject", email.Subject),
            new KeyValuePair<string, string>("text", email.PlainTextBody),
            new KeyValuePair<string, string>("html", email.HtmlBody)
        };

        var response = await SendSimpleEmailAsync(email, contentValues, cancellationToken);

        var responseContent = await response.Content.ReadAsStringAsync();
        _logger.LogInformation("Sending Mailgun message with status code {statusCode} and response: {response}", response.StatusCode, responseContent);
    }

    public async Task SendEmailAsync(EmailWithTemplate email, CancellationToken cancellationToken = default)
    {
        Guard.Against.NullOrWhiteSpace(email.TemplateId);

        var contentValues = new List<KeyValuePair<string, string>>
        {
            new KeyValuePair<string, string>("template", email.TemplateId)
        };

        if (!string.IsNullOrWhiteSpace(email.From))
        {
            contentValues.Add(new KeyValuePair<string, string>("from", email.From));
        }
        if (!string.IsNullOrWhiteSpace(email.Subject))
        {
            contentValues.Add(new KeyValuePair<string, string>("subject", email.Subject));
        }

        foreach (var kvp in email.Metadata)
        {
            var key = kvp.Key;
            var value = kvp.Value?.ToString() ?? string.Empty;
            var json = $"{{\"{key}\": \"{value}\"}}";
            contentValues.Add(new KeyValuePair<string, string>("h:X-Mailgun-Variables", json));
        }

        var response = await SendSimpleEmailAsync(email, contentValues, cancellationToken);

        var responseContent = await response.Content.ReadAsStringAsync();
        _logger.LogInformation("Sending Mailgun template message with status code {statusCode} and response: {response}", response.StatusCode, responseContent);
    }

    private async Task<HttpResponseMessage> SendSimpleEmailAsync(BaseEmailModel email, List<KeyValuePair<string, string>> contentValues, CancellationToken cancellationToken = default)
    {
        Guard.Against.NullOrWhiteSpace(email.To);

        var httpClient = _factory.CreateClient();
        var authToken = Encoding.ASCII.GetBytes($"api:{_settings.ApiKey}");
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(authToken));

        var baseUrl = _settings.BaseUrl.Trim();
        if (baseUrl.EndsWith("/"))
        {
            baseUrl = baseUrl[..^1];
        }

        var requestUrl = $"{baseUrl}/{_settings.Domain}/messages";

        contentValues.Add(new KeyValuePair<string, string>("to", email.To));
        var requestContent = new FormUrlEncodedContent(contentValues);

        return await httpClient.PostAsync(requestUrl, requestContent, cancellationToken);
    }
}