using Endatix.Samples.WebApp.ApiClient.Model.Requests;
using Endatix.Samples.WebApp.ApiClient.Model.Responses;

namespace Endatix.Samples.WebApp.ApiClient;

/// <summary>
/// Base interface for Endatix HTTP API Client
/// </summary>
public interface IEndatixClient
{
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
