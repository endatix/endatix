using Ardalis.GuardClauses;

namespace Endatix.Samples.WebApp.ApiClient.Common;

/// <summary>
/// Represents an error that occurred Endatix API call operations.
/// </summary>
public record ApiError
{
    /// <summary>
    /// Represents no error.
    /// </summary>
    public static readonly ApiError None = new(string.Empty, ErrorType.Unknown, []);

    /// <summary>
    /// Represents a null value error.
    /// </summary>
    public static readonly ApiError NullValue = new(
        "Null value was provided",
        ErrorType.Unknown);

    /// <summary>
    /// Initializes a new instance of the <see cref="ApiError"/> record.
    /// </summary>
    /// <param name="description">A description of the error.</param>
    /// <param name="type">The type of the error.</param>
    /// <param name="details">Optional details about the error.</param>
    public ApiError(string description, ErrorType type, string[]? details = default)
    {
        Description = description;
        Details = details ?? [];
        Type = type;
    }

    /// <summary>
    /// Gets the description of the error.
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// Gets the details of the error.
    /// </summary>
    public IReadOnlyList<string>? Details { get; }

    /// <summary>
    /// Gets the type of the error.
    /// </summary>
    public ErrorType Type { get; }

    /// <summary>
    /// Creates a new <see cref="ApiError"/> instance for client-side errors.
    /// </summary>
    /// <param name="description">A description of the error.</param>
    /// <param name="details">Optional details about the error.</param>
    /// <returns>A new <see cref="ApiError"/> instance.</returns>
    public static ApiError ClientError(string description, string[]? details = default) => new(description, ErrorType.AtClient, details);

    /// <summary>
    /// Creates a new <see cref="ApiError"/> instance for connection failures.
    /// </summary>
    /// <param name="client">The HTTP client.</param>
    /// <param name="additionalDetails">Additional details about the connection failure.</param>
    /// <returns>A new <see cref="ApiError"/> instance.</returns>
    public static ApiError ConnectionFailure(HttpClient client, string additionalDetails) => new(
        "Cannot connect to Endatix API",
        ErrorType.Connectivity,
        [additionalDetails, $"Please check if the Endatix.API is running on {client.BaseAddress} or change the EndatixClient:ApiBaseUrl Config Setting"]
    );

    /// <summary>
    /// Creates a new <see cref="ApiError"/> instance for unauthorized access.
    /// </summary>
    /// <returns>A new <see cref="ApiError"/> instance.</returns>
    public static ApiError Unauthorized() => new(
        "Unauthorized Access Error",
        ErrorType.Authorization,
        ["Unauthorized response from Endatix API", "Please check your access token. More info at https://docs.endatix.com/docs/getting-started/installation#step-5-configure-the-appsettings"]
    );

    /// <summary>
    /// Creates a new <see cref="ApiError"/> instance for not found errors.
    /// </summary>
    /// <returns>A new <see cref="ApiError"/> instance.</returns>
    public static ApiError NotFound() => new(
        "Resource Not Found",
        ErrorType.NotFound,
        ["Item not found. Please check the requested resource Id and try again"]
    );

    /// <summary>
    /// Creates a new <see cref="ApiError"/> instance for internal server errors.
    /// </summary>
    /// <returns>A new <see cref="ApiError"/> instance.</returns>
    public static ApiError InternalServerError() => new(
       "Endatix APIServer Error",
       ErrorType.ServerError,
       ["There was an error on the Endatix API server. Please check your Endatix.API instance and contact support if the issue persists"]
   );

    /// <summary>
    /// Creates a new <see cref="ApiError"/> instance from an HTTP response.
    /// </summary>
    /// <param name="response">The HTTP response.</param>
    /// <returns>A new <see cref="ApiError"/> instance.</returns>
    public static ApiError FromResponse(HttpResponseMessage response)
    {
        Guard.Against.Null(response);

        return response.StatusCode switch
        {
            System.Net.HttpStatusCode.Unauthorized => Unauthorized(),
            System.Net.HttpStatusCode.NotFound => NotFound(),
            System.Net.HttpStatusCode.InternalServerError => InternalServerError(),
            _ => new ApiError("Unknown Error", ErrorType.Unknown, [$"Response Status Code: {response.StatusCode}"])
        };
    }
}