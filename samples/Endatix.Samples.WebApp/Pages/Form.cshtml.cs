using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using Endatix.Samples.WebApp.ApiClient;
using Endatix.Samples.WebApp.ApiClient.Model.Responses;
using Endatix.Samples.WebApp.ApiClient.Common;

namespace Endatix.Samples.WebApp.Pages;

public class FormPage : PageModel
{
    private readonly ILogger<FormPage> _logger;
    private readonly IEndatixClient _client;

    public string FormId { get; set; } = string.Empty;
    public FormDefinitionResponse? Form { get; set; }

    public ApiError? ErrorState { get; private set; }

    public readonly string BaseUrl;

    public FormPage(ILogger<FormPage> logger, IEndatixClient client, IOptions<HttpClientOptions> options)
    {
        _logger = logger;
        _client = client;
        var settings = options.Value;

        BaseUrl = settings.ApiBaseUrl;
    }

    public async Task OnGetAsync(long id, CancellationToken cancellationToken)
    {
        FormId = id.ToString();
        var formRequestResult = await _client.GetActiveDefinitionAsync(id, cancellationToken);

        formRequestResult.Match(
            onSuccess: form =>
            {
                Form = form;
                if (_logger.IsEnabled(LogLevel.Information))
                {
                    _logger.LogInformation("Form fetching complete. Results is {@Response}", form);
                }
            },
            onError: error => ErrorState = error
        );
    }
}