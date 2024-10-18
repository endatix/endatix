using System.Net;
using System.Text.Json;
using Ardalis.GuardClauses;
using Endatix.Samples.WebApp.ApiClient.Common;
using Endatix.Samples.WebApp.ApiClient.Model;
using Endatix.Samples.WebApp.ApiClient.Model.Requests;
using Endatix.Samples.WebApp.ApiClient.Model.Responses;

namespace Endatix.Samples.WebApp.ApiClient;

/// <summary>
/// Represents the Endatix HTTP API Client, providing methods for form retrieval, active form definition retrieval, and form submission.
/// </summary>
public class EndatixClient(HttpClient client) : IEndatixClient
{
    private static readonly JsonSerializerOptions _defaultJsonSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>
    /// Initiates an asynchronous operation to retrieve a list of forms based on the provided request parameters.
    /// </summary>
    /// <param name="request">The request object containing form ID, page number, and page size.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a list of form responses.</returns>
    public async Task<ApiResult<IEnumerable<FormModel>>> GetFormsAsync(FormListRequest request, CancellationToken cancellationToken = default)
    {
        Guard.Against.Null(request, nameof(request));

        var requestMessage = CreateGetHttpRequestMessage($"forms?page={request.Page}&pageSize={request.PageSize}");
        return await SendAsJsonAsync<IEnumerable<FormModel>>(requestMessage, cancellationToken);
    }

    /// <summary>
    /// Initiates an asynchronous operation to retrieve the active form definition given a valid form ID.
    /// </summary>
    /// <param name="formId">The unique identifier of the form.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The form definition model of type: <c>FormDefinitionResponse</c></returns>
    public async Task<ApiResult<FormDefinitionResponse>> GetActiveDefinitionAsync(long formId, CancellationToken cancellationToken)
    {
        var requestMessage = CreateGetHttpRequestMessage($"forms/{formId}/definition");
        return await SendAsJsonAsync<FormDefinitionResponse>(requestMessage, cancellationToken);
    }

    /// <summary>
    /// Initiates an asynchronous operation to create a form submission given a request.
    /// </summary>
    /// <param name="submissionRequest">The request object containing the submission details.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>Returns the response of type <c>CreateSubmissionResponse</c>, or null if the operation fails.</returns>
    public async Task<CreateSubmissionResponse?> SubmitFormAsync(CreateSubmissionRequest submissionRequest, CancellationToken cancellationToken = default)
    {
        if (submissionRequest == null)
        {
            throw new ArgumentNullException(nameof(submissionRequest));
        }

        var responseMessage = await client.PostAsJsonAsync($"forms/{submissionRequest.FormId}/submissions", submissionRequest, cancellationToken).ConfigureAwait(false);

        if (!responseMessage.IsSuccessStatusCode)
        {
            throw new Exception("Request failed with status: " + responseMessage.StatusCode);
        }

        var contentStream = await responseMessage.Content.ReadAsStreamAsync(cancellationToken);
        if (contentStream == null || contentStream.Length == 0)
        {
            throw new Exception("Request failed with status: " + HttpStatusCode.InternalServerError);
        }

        try
        {
            var response = await JsonSerializer.DeserializeAsync<CreateSubmissionResponse>(contentStream, cancellationToken: cancellationToken);
            return response;
        }
        catch (JsonException)
        {
            throw new Exception("Failed to deserialize the response.");
        }
    }

    private async Task<ApiResult<TResponse>> SendAsJsonAsync<TResponse>(
    HttpRequestMessage requestMessage,
    CancellationToken cancellationToken = default, JsonSerializerOptions? options = null)
    {
        try
        {
            var response = await client.SendAsync(requestMessage, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                try
                {
                    var contentStream = await response.Content.ReadAsStreamAsync();
                    var jsonSerializerOptions = options ?? _defaultJsonSerializerOptions;

                    var result = await JsonSerializer.DeserializeAsync<TResponse>(contentStream, jsonSerializerOptions, cancellationToken);

                    if (result is { } successApiResult)
                    {
                        return successApiResult;
                    }
                    else
                    {
                        return ApiError.ClientError("Empty or invalid response content.");
                    }
                }
                catch (JsonException ex)
                {
                    return ApiError.ClientError($"Error deserializing response: {ex.Message}");
                }
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();

                return ApiError.FromResponse(response);
            }
        }
        catch (HttpRequestException ex)
        {
            return ApiError.ConnectionFailure(client, ex.Message);
        }
        catch (Exception ex)
        {
            return new ApiError($"Unexpected error: {ex.Message}", ErrorType.Unknown);
        }
    }


    private HttpRequestMessage CreateGetHttpRequestMessage(string uri) => new HttpRequestMessage(HttpMethod.Get, uri);

    private HttpRequestMessage CreateHttpRequestMessage<TPayload>(
    string uri, HttpMethod httpMethod, TPayload? payload)
    {
        Guard.Against.NullOrWhiteSpace(uri);

        var requestMessage = new HttpRequestMessage(httpMethod, uri);

        if (payload != null && ShouldIncludePayload(httpMethod))
        {
            requestMessage.Content = JsonContent.Create(payload);
        }

        return requestMessage;
    }

    private bool ShouldIncludePayload(HttpMethod httpMethod) => httpMethod == HttpMethod.Post || httpMethod == HttpMethod.Put;

}
