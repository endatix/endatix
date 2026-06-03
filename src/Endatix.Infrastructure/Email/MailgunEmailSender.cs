using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Endatix.Core;
using Endatix.Core.Abstractions;
using Endatix.Core.Features.Email;
using Ardalis.GuardClauses;

namespace Endatix.Infrastructure.Email;

/// <summary>
/// Mailgun email sender implementation.
/// </summary>
/// <remarks>
/// Initializes a new instance of the MailgunEmailSender class.
/// </remarks>
/// <param name="factory">The HTTP client factory.</param>
/// <param name="logger">The logger.</param>
/// <param name="options">The mailgun settings.</param>
/// <param name="templateRenderer">The email template renderer.</param>
public class MailgunEmailSender(
    IHttpClientFactory factory,
    ILogger<MailgunEmailSender> logger,
    IOptions<MailgunSettings> options,
    EmailTemplateRenderer templateRenderer
    ) : IEmailSender, IHasConfigSection<MailgunSettings>, IPluginInitializer
{
    private readonly IHttpClientFactory _factory = factory;

    private readonly ILogger _logger = logger;

    private readonly MailgunSettings _settings = options.Value;

    private readonly EmailTemplateRenderer _templateRenderer = templateRenderer;


    /// <inheritdoc />
    public static Action<IServiceCollection> InitializationDelegate => (services) =>
    {
        services.AddHttpClient();
    };

    public void Install(IServiceCollection services)
    {
        services.AddHttpClient();
    }

    /// <inheritdoc />
    public async Task SendEmailAsync(EmailWithBody email, CancellationToken cancellationToken = default)
    {
        Guard.Against.Null(email);
        Guard.Against.NullOrWhiteSpace(email.To);
        Guard.Against.NullOrWhiteSpace(email.From);
        Guard.Against.NullOrWhiteSpace(email.Subject);
        Guard.Against.NullOrWhiteSpace(email.PlainTextBody);
        Guard.Against.NullOrWhiteSpace(email.HtmlBody);

        var contentValues = new List<KeyValuePair<string, string>>
        {
            new("from", email.From),
            new("subject", email.Subject),
            new("text", email.PlainTextBody),
            new("html", email.HtmlBody)
        };

        var response = await SendSimpleEmailAsync(email, contentValues, cancellationToken);

        var responseContent = await response.Content.ReadAsStringAsync();
        _logger.LogInformation("Sending Mailgun message with status code {statusCode} and response: {response}", response.StatusCode, responseContent);
    }

    /// <inheritdoc />
    public async Task SendEmailAsync(EmailWithTemplate email, CancellationToken cancellationToken = default)
    {
        Guard.Against.Null(email);

        if (email.IsExternal)
        {
            await SendExternalTemplateEmailAsync(email, cancellationToken);
            return;
        }

        var emailWithBody = await _templateRenderer.RenderAsync(email, cancellationToken);

        await SendEmailAsync(emailWithBody, cancellationToken);
    }

    private async Task SendExternalTemplateEmailAsync(EmailWithTemplate email, CancellationToken cancellationToken = default)
    {
        Guard.Against.Null(email);
        Guard.Against.NullOrWhiteSpace(email.To);
        Guard.Against.NullOrWhiteSpace(email.TemplateId);

        var contentValues = new List<KeyValuePair<string, string>>
        {
            new("template", email.TemplateId)
        };

        if (!string.IsNullOrWhiteSpace(email.From))
        {
            contentValues.Add(new("from", email.From));
        }

        if (!string.IsNullOrWhiteSpace(email.Subject))
        {
            contentValues.Add(new("subject", email.Subject));
        }

        if (email.Metadata.Count > 0)
        {
            contentValues.Add(new("h:X-Mailgun-Variables", JsonSerializer.Serialize(email.Metadata)));
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
        if (baseUrl.EndsWith('/'))
        {
            baseUrl = baseUrl[..^1];
        }

        var requestUrl = $"{baseUrl}/{_settings.Domain}/messages";

        contentValues.Add(new KeyValuePair<string, string>("to", email.To));
        var requestContent = new FormUrlEncodedContent(contentValues);

        return await httpClient.PostAsync(requestUrl, requestContent, cancellationToken);
    }
}