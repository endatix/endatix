using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.Submissions.GetByAccessToken;

/// <summary>
/// Query to get a submission using an access token.
/// </summary>
/// <param name="FormId">The ID of the form (for validation)</param>
/// <param name="Token">The access token</param>
public record GetByAccessTokenQuery(long FormId, string Token) : IQuery<Result<Submission>>;
