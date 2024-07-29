using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.Submissions.Create;

/// <summary>
/// Command used for creating a form Submission entry.
/// </summary>
/// <param name="FormId"></param>
/// <param name="JsonData"></param>
/// <param name="MetaData"></param>
/// <param name="CurrentPage"></param>
/// <param name="IsComplete"></param>
public record CreateSubmissionCommand(long FormId, string JsonData, string? MetaData, int? CurrentPage, bool? IsComplete) : ICommand<Result<Submission>>;