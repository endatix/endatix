using Ardalis.GuardClauses;
using Endatix.Core.Abstractions.Submissions;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using Microsoft.Extensions.Options;

namespace Endatix.Infrastructure.Features.Submissions;

public class SubmissionTokenService : ISubmissionTokenService
{
    private readonly IRepository<Submission> _repository;
    private readonly SubmissionOptions _options;

    public SubmissionTokenService(IRepository<Submission> repository, IOptions<SubmissionOptions> options)
    {
        Guard.Against.Null(repository);
        Guard.Against.Null(options.Value);
        Guard.Against.NegativeOrZero(options.Value.TokenExpiryInHours);

        _repository = repository;
        _options = options.Value;
    }

    public async Task<Result<string>> ObtainTokenAsync(long submissionId, CancellationToken cancellationToken)
    {
        Guard.Against.NegativeOrZero(submissionId);

        var submission = await _repository.GetByIdAsync(submissionId, cancellationToken);
        if (submission == null)
        {
            return Result.NotFound("Submission not found");
        }

        if (submission.Token == null)
        {
            submission.UpdateToken(new Token(_options.TokenExpiryInHours));
        }
        else
        {
            submission.Token.Extend(_options.TokenExpiryInHours);
        }

        await _repository.SaveChangesAsync(cancellationToken);
        return Result<string>.Success(submission!.Token!.Value);
    }

    public async Task<Result<long>> ResolveTokenAsync(string token, CancellationToken cancellationToken)
    {
        Guard.Against.NullOrEmpty(token);

        var submission = await _repository.FirstOrDefaultAsync(new SubmissionByTokenSpec(token), cancellationToken);
        if (submission == null || submission.Token!.IsExpired)
        {
            return Result.NotFound("Invalid or expired token");
        }

        if (submission.IsComplete)
        {
            return Result.NotFound("Submission completed");
        }

        return Result<long>.Success(submission.Id);
    }
}
