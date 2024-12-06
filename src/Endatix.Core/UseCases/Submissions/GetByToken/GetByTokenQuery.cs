using Ardalis.GuardClauses;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.Submissions.GetByToken;

/// <summary>
/// Query for getting a submission by FormId and Token
/// </summary>
public record GetByTokenQuery : IQuery<Result<Submission>>
{
    public long FormId { get; init; }
    public string Token { get; init; }

    public GetByTokenQuery(long formId, string token)
    {
        Guard.Against.NegativeOrZero(formId);
        Guard.Against.NullOrEmpty(token);

        FormId = formId;
        Token = token;
    }
}
