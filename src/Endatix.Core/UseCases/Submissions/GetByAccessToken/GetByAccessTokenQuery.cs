using Ardalis.GuardClauses;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.Submissions.GetByAccessToken;

/// <summary>
/// Query to get a submission using an access token.
/// </summary>
public record GetByAccessTokenQuery : IQuery<Result<Submission>>
{
    public long FormId { get; init; }
    public string Token { get; init; }

    public GetByAccessTokenQuery(long formId, string token)
    {
        Guard.Against.NegativeOrZero(formId);
        Guard.Against.NullOrEmpty(token);

        FormId = formId;
        Token = token;
    }
}
