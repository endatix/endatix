using Ardalis.GuardClauses;
using Endatix.Core.Abstractions.Submissions;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;

namespace Endatix.Infrastructure.Features.Submissions;

public class SubmissionTokenService : ISubmissionTokenService
{
    private readonly IRepository<Submission> _submissionRepository;
    private readonly IRepository<TenantSettings> _tenantSettingsRepository;

    public SubmissionTokenService(
        IRepository<Submission> submissionRepository,
        IRepository<TenantSettings> tenantSettingsRepository)
    {
        Guard.Against.Null(submissionRepository);
        Guard.Against.Null(tenantSettingsRepository);

        _submissionRepository = submissionRepository;
        _tenantSettingsRepository = tenantSettingsRepository;
    }

    public async Task<Result<string>> ObtainTokenAsync(long submissionId, CancellationToken cancellationToken)
    {
        Guard.Against.NegativeOrZero(submissionId);

        var submission = await _submissionRepository.GetByIdAsync(submissionId, cancellationToken);
        if (submission == null)
        {
            return Result.NotFound("Submission not found");
        }

        var tokenExpiryHours = await GetTokenExpiryHoursAsync(submission.TenantId, cancellationToken);

        if (submission.Token == null)
        {
            submission.UpdateToken(new Token(tokenExpiryHours));
        }
        else
        {
            submission.Token.Extend(tokenExpiryHours);
        }

        await _submissionRepository.SaveChangesAsync(cancellationToken);
        return Result<string>.Success(submission!.Token!.Value);
    }

    public async Task<Result<long>> ResolveTokenAsync(string token, CancellationToken cancellationToken)
    {
        Guard.Against.NullOrEmpty(token);

        var submission = await _submissionRepository.FirstOrDefaultAsync(new SubmissionByTokenSpec(token), cancellationToken);
        if (submission == null || submission.Token!.IsExpired)
        {
            return Result.NotFound("Invalid or expired token");
        }

        if (submission.IsComplete)
        {
            var tenantSettings = await _tenantSettingsRepository
                .FirstOrDefaultAsync(new TenantSettingsByTenantIdSpec(submission.TenantId), cancellationToken);
            Guard.Against.Null(tenantSettings, "Tenant settings must be configured.");

            if (!tenantSettings.IsSubmissionTokenValidAfterCompletion)
            {
                return Result.NotFound("Submission completed");
            }
        }

        return Result<long>.Success(submission.Id);
    }

    private async Task<int?> GetTokenExpiryHoursAsync(long tenantId, CancellationToken cancellationToken)
    {
        var tenantSettings = await _tenantSettingsRepository
            .FirstOrDefaultAsync(new TenantSettingsByTenantIdSpec(tenantId), cancellationToken);
        Guard.Against.Null(tenantSettings, "Tenant settings must be configured.");

        return tenantSettings.SubmissionTokenExpiryHours;
    }
}
