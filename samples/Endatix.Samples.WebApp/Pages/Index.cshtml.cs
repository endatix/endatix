using Microsoft.AspNetCore.Mvc.RazorPages;
using Endatix.Samples.WebApp.ApiClient;
using Models = Endatix.Samples.WebApp.ApiClient.Model;
using Endatix.Samples.WebApp.ApiClient.Common;

namespace Endatix.Samples.WebApp.Pages;
public class IndexModel : PageModel
{
    private readonly ILogger<IndexModel> _logger;
    private readonly IEndatixClient _client;

    public IEnumerable<Models.FormModel> Forms { get; private set; } = [];

    public ApiError? ErrorState { get; private set; }

    public IndexModel(ILogger<IndexModel> logger, IEndatixClient client)
    {
        _logger = logger;
        _client = client;
    }

    public async Task OnGetAsync()
    {
        var formsApiResult = await _client.GetFormsAsync(new());

        formsApiResult.Match(
            onSuccess: forms => Forms = forms,
            onError: error => ErrorState = error
            );
    }
}
