using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;

namespace Endatix.Core.UseCases.Forms;

internal readonly record struct SingleSubmissionGateChange(long FormId, long TenantId, bool WasLimited, bool Limit, bool Public)
{
    internal async Task<Result<Form>?> ConflictAsync(IRepository<Submission> submissions, CancellationToken ct)
    {
        if (WasLimited && !Limit)
        {
            return Result<Form>.Conflict("Single submission gate cannot be disabled after it has been enabled.");
        }
        if (Public && Limit)
        {
            return Result<Form>.Conflict("A single-submission form cannot be made public.");
        }
        if (!WasLimited && Limit && await HasDuplicatesAsync(submissions, ct))
        {
            return Result<Form>.Conflict("Cannot enable single submission gate because this form already has duplicate submissions.");
        }
        return null;
    }

    private async Task<bool> HasDuplicatesAsync(IRepository<Submission> submissions, CancellationToken ct)
    {
        var submitters = await submissions.ListAsync(new EligibleSingleSubmissionGateSubmitterIdsByFormIdSpec(FormId, TenantId), ct);
        return submitters.Where(id => !string.IsNullOrWhiteSpace(id)).GroupBy(id => id).Any(group => group.Count() > 1);
    }
}
