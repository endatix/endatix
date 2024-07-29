using System;
using System.Threading;
using System.Threading.Tasks;
using SendGrid;
using SendGrid.Helpers.Mail;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Endatix.Core.Abstractions;
using Endatix.Core.Features.Email;
using SendGrid.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Endatix.Core;

namespace Endatix.Infrastructure.Email;

public class SendGridEmailSender : IEmailSender, IHasConfigSection<SendGridSettings>, IPluginInitializer
{
    private readonly ISendGridClient _sendGridClient;

    private readonly ILogger _logger;

    private readonly SendGridSettings _settings;

    public SendGridEmailSender(ISendGridClient sendGridClient, ILogger<SendGridEmailSender> logger, IOptions<SendGridSettings> options)
    {
        _sendGridClient = sendGridClient;
        _logger = logger;
        _settings = options.Value;
    }

    public static Action<IServiceCollection> InitializationDelegate => (services) =>
    {
        services.AddSendGrid((provider, options) =>
       {
           SendGridSettings? settings = provider.GetService<IOptions<SendGridSettings>>()?.Value;
           if (settings != null)
           {
               options.ApiKey = settings.ApiKey;
           };
       });
    };

    public async Task SendEmailAsync(EmailWithBody email, CancellationToken cancellationToken = default)
    {
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

    public async Task SendEmailAsync(EmailWithTemplate email, CancellationToken cancellationToken = default)
    {
        var msg = new SendGridMessage();

        msg.SetFrom(new EmailAddress(email.From));
        msg.AddTo(new EmailAddress(email.To));
        msg.SetTemplateId(email.TemplateId);

        var dynamicTemplateData = new ExampleTemplateData();
        if (email.Metadata.TryGetValue("name", out var name))
        {
            dynamicTemplateData.Name = (string)name;
        }
        msg.SetTemplateData(dynamicTemplateData);

        var response = await _sendGridClient
                .SendEmailAsync(msg, cancellationToken)
                .ConfigureAwait(false);

        var responseBody = await response.Body.ReadAsStringAsync();
        _logger.LogInformation("Sending SendGrid message with status code {statusCode} and response: {response}", response.StatusCode, responseBody);
    }

    private class ExampleTemplateData
    {
        [JsonProperty("name")]
        public string Name { get; set; }

    }
}
