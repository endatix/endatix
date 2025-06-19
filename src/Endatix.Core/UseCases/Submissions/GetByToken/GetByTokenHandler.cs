using MediatR;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.Submissions.GetById;
using Endatix.Core.Abstractions.Submissions;

namespace Endatix.Core.UseCases.Submissions.GetByToken;

/// <summary>
/// Handler for getting a submission by token
/// </summary>
public class GetByTokenHandler(ISender sender, ISubmissionTokenService tokenService) : IQueryHandler<GetByTokenQuery, Result<Submission>>
{
    public async Task<Result<Submission>> Handle(GetByTokenQuery request, CancellationToken cancellationToken)
    {
        var tokenResult = await tokenService.ResolveTokenAsync(request.Token, cancellationToken);
        if (!tokenResult.IsSuccess)
        {
            return Result.NotFound("Invalid or expired token");
        }

        var submissionId = tokenResult.Value;
        var getByIdQuery = new GetByIdQuery(request.FormId, submissionId);
        return await sender.Send(getByIdQuery, cancellationToken);
    }
}
