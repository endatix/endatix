namespace Endatix.Api.Endpoints.Integrations;

/// <summary>
/// Request model for creating a form with an active form definition.
/// </summary>
public class SlackTokenRequest
{
    /// <summary>
    /// The name of the form.
    /// </summary>
    public string? Token { get; set; }
}
