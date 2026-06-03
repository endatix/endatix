using SendGrid;
using SendGrid.Helpers.Mail;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Endatix.Core;
using Endatix.Core.Abstractions;
using Endatix.Core.Features.Email;
using SendGrid.Extensions.DependencyInjection;
using Ardalis.GuardClauses;

namespace Endatix.Infrastructure.Email;

/// <summary>
/// Initializes a new instance of the SendGridEmailSender class.
/// </summary>
/// <param name="sendGridClient">The SendGrid client.</param>
/// <param name="logger">The logger.</param>
/// <param name="options">The sendgrid settings.</param>
/// <param name="templateRenderer">The email template renderer.</param>
public class SendGridEmailSender(
        ISendGridClient sendGridClient,
        ILogger<SendGridEmailSender> logger,
        IOptions<SendGridSettings> options,
        EmailTemplateRenderer templateRenderer
      ) : IEmailSender, IHasConfigSection<SendGridSettings>, IPluginInitializer
{
    private readonly ISendGridClient _sendGridClient = sendGridClient;

    private readonly ILogger _logger = logger;

    private readonly SendGridSettings _settings = options.Value;

    private readonly EmailTemplateRenderer _templateRenderer = templateRenderer;

    /// <inheritdoc />
    public static Action<IServiceCollection> InitializationDelegate => (services) =>
    {
        services.PostConfigure<SendGridSettings>(settings =>
        {
            if (string.IsNullOrWhiteSpace(settings.ApiKey))
            {
                settings.ApiKey = Environment.GetEnvironmentVariable("SENDGRID_API_KEY");
            }
        });

        services.AddSendGrid((provider, options) =>
        {
            var settings = provider.GetRequiredService<IOptions<SendGridSettings>>().Value;
            options.ApiKey = settings.ApiKey;
        });
    };

    /// <inheritdoc />
    public async Task SendEmailAsync(EmailWithBody email, CancellationToken cancellationToken = default)
    {
        Guard.Against.Null(email);
        Guard.Against.NullOrWhiteSpace(email.To);
        Guard.Against.NullOrWhiteSpace(email.From);
        Guard.Against.NullOrWhiteSpace(email.Subject);

        var fromEmail = new EmailAddress(email.From);
        var toEmail = new EmailAddress(email.To);
        var msg = MailHelper.CreateSingleEmail(
            fromEmail,
            toEmail,
            email.Subject,
            email.PlainTextBody,
            email.HtmlBody
        );

        var response = await _sendGridClient
                    .SendEmailAsync(msg, cancellationToken)
                    .ConfigureAwait(false);

        var responseBody = await response.Body.ReadAsStringAsync();
        _logger.LogInformation("Sending SendGrid message with status code {statusCode} and response: {response}", response.StatusCode, responseBody);
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
        Guard.Against.NullOrWhiteSpace(email.From);
        Guard.Against.NullOrWhiteSpace(email.TemplateId);

        var msg = new SendGridMessage();
        msg.SetFrom(new EmailAddress(email.From));
        msg.AddTo(new EmailAddress(email.To));
        msg.SetTemplateId(email.TemplateId);
        msg.SetTemplateData(email.Metadata);

        var response = await _sendGridClient
            .SendEmailAsync(msg, cancellationToken)
            .ConfigureAwait(false);

        var responseBody = await response.Body.ReadAsStringAsync();
        _logger.LogInformation("Sending SendGrid template message with status code {statusCode} and response: {response}", response.StatusCode, responseBody);
    }
}
