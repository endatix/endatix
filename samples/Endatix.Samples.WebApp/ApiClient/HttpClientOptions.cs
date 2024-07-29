using System.ComponentModel.DataAnnotations;

namespace Endatix.Samples.WebApp.ApiClient;

/// <summary>
/// Configuration options for the Endatix Http Client
/// </summary>
public class HttpClientOptions
{
    /// <summary>
    /// Const for the AppSettings section key that will be used to store the Endatix Client config options
    /// </summary>
    public const string CONFIG_SECTION_KEY = "EndatixClient";

    /// <summary>
    /// Base url for the Endatix API
    /// </summary>
    [Required]
    public required string ApiBaseUrl { get; set; }

    /// <summary>
    /// The Form Id of the Contact Us form
    /// </summary>
    [Required]
    public required long ContactUsFormId { get; set; }
}
