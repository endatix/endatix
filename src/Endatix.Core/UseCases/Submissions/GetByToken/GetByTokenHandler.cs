using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Abstractions.Submissions;
using Endatix.Core.Specifications;

namespace Endatix.Core.UseCases.Submissions.GetByToken;

/// <summary>
/// Handler for getting a submission by token
/// </summary>
public class GetByTokenHandler(
    IRepository<Submission> repository,
    ISubmissionTokenService tokenService) : IQueryHandler<GetByTokenQuery, Result<Submission>>
{
    public async Task<Result<Submission>> Handle(GetByTokenQuery request, CancellationToken cancellationToken)
    {
        var tokenResult = await tokenService.ResolveTokenAsync(request.Token, cancellationToken);
        if (!tokenResult.IsSuccess)
        {
            return Result.Invalid(SubmissonTokenErrors.ValidationErrors.SubmissionTokenInvalid);
        }

        var submissionId = tokenResult.Value;

        var submissionSpec = new SubmissionWithDefinitionAndFormSpec(request.FormId, submissionId);
        var submission = await repository.SingleOrDefaultAsync(submissionSpec, cancellationToken);

        if (submission == null)
        {
            return Result.NotFound("Submission not found");
        }

        if (!submission.Form.IsEnabled)
        {
            return Result.NotFound("Form not found");
        }

        return Result.Success(submission);
    }
}
