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
    private readonly IRepository<Form> _formRepository;
    private readonly IRepository<TenantSettings> _tenantSettingsRepository;

    public SubmissionTokenService(
        IRepository<Submission> submissionRepository,
        IRepository<Form> formRepository,
        IRepository<TenantSettings> tenantSettingsRepository)
    {
        Guard.Against.Null(submissionRepository);
        Guard.Against.Null(formRepository);
        Guard.Against.Null(tenantSettingsRepository);

        _submissionRepository = submissionRepository;
        _formRepository = formRepository;
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

        var formExpiry = await _formRepository.FirstOrDefaultAsync(
            new FormProjections.SessionTokenExpiryDtoSpec(submission.FormId), cancellationToken);
        if (formExpiry == null)
        {
            // A live submission can outlast its form when a delete misses it; that is not found, not a 500.
            return Result.NotFound("Form not found");
        }

        IssueOrExtend(submission, await GetTokenExpiryHoursAsync(formExpiry, submission.TenantId, cancellationToken));
        await _submissionRepository.SaveChangesAsync(cancellationToken);
        return Result<string>.Success(submission!.Token!.Value);
    }

    public async Task<Token> NewTokenAsync(Form form, CancellationToken cancellationToken)
    {
        Guard.Against.Null(form);

        var formExpiry = new FormProjections.FormSessionTokenExpiryDto(form.Id, form.SubmissionTokenExpiryHours);
        return new Token(await GetTokenExpiryHoursAsync(formExpiry, form.TenantId, cancellationToken));
    }

    private static void IssueOrExtend(Submission submission, int? tokenExpiryHours)
    {
        if (submission.Token == null)
        {
            submission.UpdateToken(new Token(tokenExpiryHours));
        }
        else
        {
            submission.Token.Extend(tokenExpiryHours);
        }
    }

    public async Task<Result<long>> ResolveTokenAsync(string token, CancellationToken cancellationToken)
    {
        Guard.Against.NullOrEmpty(token);

        var submission = await _submissionRepository.FirstOrDefaultAsync(new SubmissionByTokenSpec(token), cancellationToken);
        if (submission == null || submission.Token!.IsExpired)
        {
            return Result.NotFound("Invalid or expired token");
        }

        // A screened-out interview has ended too, so its token follows the same after-completion rule.
        if (submission.IsComplete || submission.IsScreenedOut)
        {
            // No settings row reads as a new tenant's default: not valid after completion.
            var tenantSettings = await _tenantSettingsRepository
                .FirstOrDefaultAsync(new TenantSettingsByTenantIdSpec(submission.TenantId), cancellationToken);

            if (tenantSettings?.IsSubmissionTokenValidAfterCompletion != true)
            {
                return Result.NotFound(submission.IsComplete ? "Submission completed" : "Submission screened out");
            }
        }

        return Result<long>.Success(submission.Id);
    }

    private async Task<int?> GetTokenExpiryHoursAsync(
        FormProjections.FormSessionTokenExpiryDto formExpiry,
        long tenantId,
        CancellationToken cancellationToken)
    {
        if (formExpiry.SubmissionTokenExpiryHours.HasValue)
        {
            return formExpiry.SubmissionTokenExpiryHours;
        }

        // A tenant created before tenant settings existed has no row. It gets a new tenant's default
        // instead of failing every submission create with a 500.
        var tenantSettings = await _tenantSettingsRepository
            .FirstOrDefaultAsync(new TenantSettingsByTenantIdSpec(tenantId), cancellationToken);
        return tenantSettings is null
            ? TenantSettings.DefaultSubmissionTokenExpiryHours
            : tenantSettings.SubmissionTokenExpiryHours;
    }
}
