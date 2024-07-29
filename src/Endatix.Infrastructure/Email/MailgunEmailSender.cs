using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Endatix.Core;
using Endatix.Core.Abstractions;
using Endatix.Core.Features.Email;

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

    public Task SendEmailAsync(EmailWithBody email, CancellationToken cancellationToken = default)
    {
        return SendSimpleEmailAsync(email);
    }

    public Task SendEmailAsync(EmailWithTemplate email, CancellationToken cancellationToken = default)
    {
        return SendSimpleEmailAsync(email);
    }

    private async Task SendSimpleEmailAsync(BaseEmailModel email)
    {
        HttpClient httpClient = _factory.CreateClient("github");
        var authToken = Encoding.ASCII.GetBytes($"api:{_settings.ApiKey}");
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(authToken));

        var baseUrl = _settings.BaseUrl.Trim();
        if (baseUrl.EndsWith("/"))
        {
            baseUrl = baseUrl.Substring(0, baseUrl.Length - 1);
        }

        var requestUrl = $"{baseUrl}/{_settings.Domain}/messages";

        var requestContent = new FormUrlEncodedContent(new[]
       {
            new KeyValuePair<string, string>("from", "tech@endatix.com"),
            new KeyValuePair<string, string>("to", email.To),
            new KeyValuePair<string, string>("subject", email.Subject),
            new KeyValuePair<string, string>("template", _settings.WelcomeEmailTemplateName),
            new KeyValuePair<string, string>("h:X-Mailgun-Variables", "{\"firstname\": \"Oggy\"}")
       });

        var response = await httpClient.PostAsync(requestUrl, requestContent);
    }
}