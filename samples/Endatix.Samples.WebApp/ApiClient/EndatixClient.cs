
using System.Net;
using System.Text.Json;
using Endatix.Samples.WebApp.ApiClient.Model;
using Endatix.Samples.WebApp.ApiClient.Model.Requests;
using Endatix.Samples.WebApp.ApiClient.Model.Responses;

namespace Endatix.Samples.WebApp.ApiClient;

public class EndatixClient(HttpClient client) : IEndatixClient
{
    private readonly HttpClient _client = client;

    public async Task<IEnumerable<FormModel>> GetFormsAsync(FormListRequest request, CancellationToken cancellationToken = default)
    {
        var formsList = await _client.GetFromJsonAsync<IEnumerable<FormModel>>($"forms?page={request.Page}&pageSize={request.PageSize}", cancellationToken);

        if (formsList is not { })
        {
            return [];
        }

        return formsList;
    }

    public async Task<FormDefinitionResponse?> GetActiveDefinitionAsync(long formId, CancellationToken cancellationToken)
    {
        FormDefinitionResponse? activeFormDefinition = await _client.GetFromJsonAsync<FormDefinitionResponse>($"forms/{formId}/definition", cancellationToken);

        return activeFormDefinition;
    }

    public async Task<CreateSubmissionResponse?> SubmitFormAsync(CreateSubmissionRequest submissionRequest, CancellationToken cancellationToken = default)
    {
        if (submissionRequest == null)
        {
            throw new ArgumentNullException(nameof(submissionRequest));
        }

        var responseMessage = await _client.PostAsJsonAsync($"forms/{submissionRequest.FormId}/submissions", submissionRequest, cancellationToken).ConfigureAwait(false);

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
}
