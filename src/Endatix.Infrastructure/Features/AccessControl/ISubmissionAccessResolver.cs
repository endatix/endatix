using Endatix.Core.Abstractions.Submissions;
using Endatix.Core.Authorization.Models;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Infrastructure.Features.AccessControl;

/// <summary>
/// Resolves submission access data based on the given context. Covers the different access scenarios and logic paths to resolve the access data for public access scenarios (no backend management).
/// </summary>
public interface ISubmissionAccessResolver
{
    /// <summary>
    /// Resolves the access data using an access token with encoded submission id and permissions in it.
    /// </summary>
    /// <param name="context">The context of the submission access</param>
    /// <param name="cancellationToken"></param>
    /// <returns>The access data for the access token</returns>
    ValueTask<Result<SubmissionAccessData>> ResolveForAccessToken(SubmissionAccessContext context, CancellationToken cancellationToken);

    /// <summary>
    /// Resolves the access data for a submission token. The submission token is a long-lived token stored on the submission and used for respondent partial submissions.
    /// </summary>
    /// <param name="context">The context of the submission access</param>
    /// <param name="cancellationToken"></param>
    /// <returns>The access data for the submission token</returns>
    ValueTask<Result<SubmissionAccessData>> ResolveForSubmissionTokenAsync(SubmissionAccessContext context, CancellationToken cancellationToken);

    /// <summary>
    /// Resolves the access data for a public form. This is the access logic for forms that are public.
    /// </summary>
    /// <param name="context">The context of the submission access</param>
    /// <param name="cancellationToken"></param>
    /// <returns>The access data for the public form</returns>
    ValueTask<Result<SubmissionAccessData>> ResolveForPublicFormAsync(SubmissionAccessContext context, CancellationToken cancellationToken);


    /// <summary>
    /// Resolves the access data for a private form. This is the default access logic for forms that are not public.
    /// </summary>
    /// <param name="context">The context of the submission access</param>
    /// <param name="cancellationToken"></param>
    /// <returns>The access data for the private form</returns>
    ValueTask<Result<SubmissionAccessData>> ResolveForPrivateFormAsync(SubmissionAccessContext context, CancellationToken cancellationToken);

    /// <summary>
    /// Checks if the form is public. This is important to determine the access logic for the form.
    /// </summary>
    /// <param name="formId">The form ID to check if it is public</param>
    /// <param name="cancellationToken"></param>
    /// <returns>The Form.IsPublic property from the database</returns>
    ValueTask<bool> ResolveIsFormPublicAsync(long formId, CancellationToken cancellationToken);
}