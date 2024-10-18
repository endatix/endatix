using Microsoft.AspNetCore.Mvc.RazorPages;
using Endatix.Core.Entities;

namespace Endatix.Samples.WebApp.Pages;

public class EditModel : PageModel
{
    private readonly ILogger<IndexModel> _logger;
    private readonly IConfiguration _configuration;

    public Form Form = new Form();

    public string BaseUrl = string.Empty;

    public EditModel(ILogger<IndexModel> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    public void OnGet(long id)
    {
        Form = new Form(){
            Id = id
        };

        BaseUrl = _configuration["EndatixSettings:ApiBaseUrl"] ?? string.Empty;
    }
}