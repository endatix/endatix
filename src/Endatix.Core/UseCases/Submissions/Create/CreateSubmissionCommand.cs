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
/// <param name="SubmittedBy"></param>
/// <param name="RequiredPermission"></param>
public record CreateSubmissionCommand(
    long FormId,
    string? JsonData,
    string? Metadata,
    int? CurrentPage,
    bool? IsComplete,
    string? ReCaptchaToken,
    string? SubmittedBy,
    string RequiredPermission
) : ICommand<Result<Submission>>;