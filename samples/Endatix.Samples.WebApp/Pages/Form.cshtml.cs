using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Endatix.Samples.WebApp.Pages;

public class FormModel : PageModel
{
    private readonly ILogger<IndexModel> _logger;
    private readonly IEndatixPlatform _endatix;

    public string FormId { get; set; }
    public FormDefinitionResponse Form { get; set; }

    public FormModel(ILogger<IndexModel> logger, IEndatixPlatform endatix)
    {
        _logger = logger;
        _endatix = endatix;
    }

    public async Task OnGetAsync(long id, CancellationToken cancellationToken)
    {
        FormId = id.ToString();
        
        FormDefinitionResponse form = await _endatix.Forms.GetActiveDefinitionAsync(id, cancellationToken);

        _logger.LogInformation("Form fetching complete. Results is {@response}", form);
        if (form != null)
        {
            Form = form;
        }
    }
}