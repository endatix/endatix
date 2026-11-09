using System.Security.Claims;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.Submissions.Open;

/// <summary>
/// The respondent opened the form. With <paramref name="SubmissionToken"/> or <paramref name="AccessToken"/> it
/// resumes that submission; with neither it creates a submission through the public create gate.
/// </summary>
/// <param name="FormId">The form the respondent opened.</param>
/// <param name="SubmissionToken">The token of the submission the respondent resumes, or null.</param>
/// <param name="AccessToken">A share link's access token with the <c>submit</c> permission, or null.</param>
/// <param name="SubmitterPrincipal">The caller, for the form's access and one-response checks on a new submission.</param>
public sealed record OpenSubmissionCommand(
    long FormId,
    string? SubmissionToken,
    string? AccessToken = null,
    ClaimsPrincipal? SubmitterPrincipal = null) : ICommand<Result<Submission>>;
