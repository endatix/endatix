using Endatix.Samples.WebApp.ApiClient.Model;
using Endatix.Samples.WebApp.ApiClient.Model.Requests;
using Endatix.Samples.WebApp.ApiClient.Model.Responses;

namespace Endatix.Samples.WebApp.ApiClient;

/// <summary>
/// Base interface for Endatix HTTP API Client
/// </summary>
public interface IEndatixClient
{
    /// <summary>
    /// Retrieves a list of forms based on the provided request parameters.
    /// </summary>
    /// <param name="request">The request object containing form ID, page number, and page size.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a list of form responses.</returns>
    Task<IEnumerable<FormModel>> GetFormsAsync(FormListRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the Active form definition given valid form Id
    /// </summary>
    /// <param name="formId"></param>
    /// <param name="cancellationToken"></param>
    /// <returns>The form definition model of type: <c>FormDefinitionModel</c></returns>
    Task<FormDefinitionResponse?> GetActiveDefinitionAsync(long formId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a Form submission given a request
    /// </summary>
    /// <param name="createSubmissionRequest"></param>
    /// <param name="cancellationToken"></param>
    /// <returns>Returns response of type <c>CreateSubmissionRequest</c></returns>
    Task<CreateSubmissionResponse?> SubmitFormAsync(CreateSubmissionRequest submissionRequest, CancellationToken cancellationToken);
}
