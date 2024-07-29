using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using Endatix.Core.Entities;
using Endatix.Samples.WebApp.ApiClient;

namespace Endatix.Samples.WebApp.Pages;
public class IndexModel : PageModel
{
    private readonly ILogger<IndexModel> _logger;
    private readonly HttpClientOptions _settings;

    public List<Form> Forms { get; set; }

    public IndexModel(ILogger<IndexModel> logger, IOptions<HttpClientOptions> options)
    {
        _logger = logger;
        _settings = options.Value;
    }

    public async Task OnGetAsync()
    {
        Forms = [
           new(){Id = _settings.ContactUsFormId, Name = "Contact Us Form"}
       ];
    }
}
