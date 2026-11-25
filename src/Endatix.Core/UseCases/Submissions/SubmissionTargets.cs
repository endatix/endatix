using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Specifications;

namespace Endatix.Core.UseCases.Submissions;

internal sealed class SubmissionTargets(IRepository<Submission> rows, ContentQueryScope scope)
{
    internal async Task<bool> CanIssueTokenAsync(long form, long submission, CancellationToken ct)
    {
        if (scope.TenantId is not long tenant)
        {
            return await rows.SingleOrDefaultAsync(new SubmissionWithDefinitionSpec(form, submission), ct) is not null;
        }
        var visible = new SubmissionByFormIdAndSubmissionIdSpec(form, submission).OnLiveForm(tenant);
        return tenant > 0 && await rows.AnyAsync(visible, ct);
    }
}
