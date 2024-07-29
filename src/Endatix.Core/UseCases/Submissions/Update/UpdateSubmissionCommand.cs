using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.Submissions;

/// <summary>
/// Command for updating a form submission.
/// </summary>
public record UpdateSubmissionCommand(long SubmissionId, long FormId, bool? IsComplete, int? CurrentPage, string JsonData, string? Metadata) : ICommand<Result<Submission>>
{

}
