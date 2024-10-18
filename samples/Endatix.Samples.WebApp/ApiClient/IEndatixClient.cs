using Endatix.Samples.WebApp.ApiClient.Common;
using Endatix.Samples.WebApp.ApiClient.Model;
using Endatix.Samples.WebApp.ApiClient.Model.Requests;
using Endatix.Samples.WebApp.ApiClient.Model.Responses;

namespace Endatix.Samples.WebApp.ApiClient;

/// <summary>
/// Defines the contract for the Endatix HTTP API Client, providing methods for form retrieval, active form definition retrieval, and form submission.
/// </summary>
public interface IEndatixClient
{
    /// <summary>
    /// Initiates an asynchronous operation to retrieve a list of forms based on the provided request parameters.
    /// </summary>
    /// <param name="request">The request object containing form ID, page number, and page size.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a list of form responses.</returns>
    Task<ApiResult<IEnumerable<FormModel>>> GetFormsAsync(FormListRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Initiates an asynchronous operation to retrieve the active form definition given a valid form ID.
    /// </summary>
    /// <param name="formId">The unique identifier of the form.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The form definition model of type: <c>FormDefinitionResponse</c></returns>
    Task<ApiResult<FormDefinitionResponse>> GetActiveDefinitionAsync(long formId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Initiates an asynchronous operation to create a form submission given a request.
    /// </summary>
    /// <param name="submissionRequest">The request object containing the submission details.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>Returns the response of type <c>CreateSubmissionResponse</c>, or null if the operation fails.</returns>
    Task<CreateSubmissionResponse?> SubmitFormAsync(CreateSubmissionRequest submissionRequest, CancellationToken cancellationToken);
}
