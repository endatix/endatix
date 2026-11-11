using Microsoft.AspNetCore.Mvc.RazorPages;
using Endatix.Core.Entities;

namespace Endatix.Samples.WebApp.Pages;

public class EditModel : PageModel
{
    private readonly IConfiguration _configuration;

    public Form? Form { get; set; }

    public string BaseUrl { get; set; } = string.Empty;

    public EditModel(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public void OnGet(long id)
    {
        BaseUrl = _configuration["EndatixSettings:ApiBaseUrl"] ?? string.Empty;
    }
}