using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using Endatix.Samples.WebApp.ApiClient;
using Endatix.Samples.WebApp.ApiClient.Model.Responses;

namespace Endatix.Samples.WebApp.Pages;

public class FormModel : PageModel
{
    private readonly ILogger<IndexModel> _logger;
    private readonly IEndatixClient _client;
    private readonly HttpClientOptions _settings;

    public string FormId { get; set; }
    public FormDefinitionResponse Form { get; set; }

    public readonly string BaseUrl;

    public FormModel(ILogger<IndexModel> logger, IEndatixClient client, IOptions<HttpClientOptions> options)
    {
        _logger = logger;
        _client = client;
        _settings = options.Value;

        BaseUrl = _settings.ApiBaseUrl;
    }

    public async Task OnGetAsync(long id, CancellationToken cancellationToken)
    {
        FormId = id.ToString();
        FormDefinitionResponse form = await _client.GetActiveDefinitionAsync(id, cancellationToken);

        _logger.LogInformation("Form fetching complete. Results is {@response}", form);
        if (form != null)
        {
            Form = form;
        }
    }
}