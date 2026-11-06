using System.Security.Claims;
using Endatix.Core.Abstractions.Submitters;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.Submissions.Create;

/// <summary>
/// Command used for creating a form Submission entry.
/// </summary>
/// <param name="FormId"></param>
/// <param name="JsonData"></param>
/// <param name="Metadata"></param>
/// <param name="CurrentPage"></param>
/// <param name="IsComplete"></param>
/// <param name="ReCaptchaToken"></param>
/// <param name="RequiredPermission"></param>
/// <param name="SubmitterPrincipal"></param>
/// <param name="Submitter"></param>
/// <param name="CollectionOutcome">
/// Optional collection outcome: a registered closed-phase status (<see cref="CollectionOutcomes"/>). It ends the
/// interview like a completion, and the submission is created in it.
/// </param>
/// <param name="PersonalizationSnapshot">Frozen audience values for a trusted on-behalf create.</param>
/// <param name="OpenSubmission">
/// The respondent's open creates the submission: it is saved <c>viewed</c>, with its start recorded
/// (<see cref="SubmissionCreateArgs.OpenSubmission"/>).
/// </param>
public record CreateSubmissionCommand(
    long FormId,
    string? JsonData,
    string? Metadata,
    int? CurrentPage,
    bool? IsComplete,
    string? ReCaptchaToken,
    string RequiredPermission,
    ClaimsPrincipal? SubmitterPrincipal = null,
    SubmitterInput? Submitter = null,
    string? CollectionOutcome = null,
    string? PersonalizationSnapshot = null,
    bool OpenSubmission = false
) : ICommand<Result<Submission>>;