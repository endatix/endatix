using System.ComponentModel.DataAnnotations;

namespace Endatix.Samples.WebApp.ApiClient.Common;

/// <summary>
/// Common error types for the responses from the Endatix API
/// </summary>
public enum ErrorType
{
    [Display(Name = "Unknown Error")]
    Unknown = 0,

    [Display(Name = "Connectivity Issue")]
    Connectivity = 1,

    [Display(Name = "Bad Request")]
    BadRequest = 2,

    [Display(Name = "Validation Failure")]
    Validation = 3,

    [Display(Name = "Server Error")]
    ServerError = 4,

    [Display(Name = "Resource Not Found")]
    NotFound = 5,

    [Display(Name = "Authentication Error")]
    Authentication = 6,

    [Display(Name = "Authorization Error")]
    Authorization = 7,

    [Display(Name = "Conflict")]
    Conflict = 8,

    [Display(Name = "API Client Error")]
    AtClient = 9
}