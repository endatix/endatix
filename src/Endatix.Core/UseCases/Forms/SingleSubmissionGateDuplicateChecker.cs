using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Specifications;

namespace Endatix.Core.UseCases.Forms;

internal static class SingleSubmissionGateDuplicateChecker
{
    public static async Task<bool> HasDuplicateEligibleSubmissionsAsync(
        long formId,
        IRepository<Submission> submissionRepository,
        CancellationToken cancellationToken)
    {
        var eligibleSubmitterIds = await submissionRepository.ListAsync(
            new EligibleSingleSubmissionGateSubmitterIdsByFormIdSpec(formId),
            cancellationToken);

        return eligibleSubmitterIds
            .Where(submittedBy => !string.IsNullOrWhiteSpace(submittedBy))
            .GroupBy(submittedBy => submittedBy)
            .Any(group => group.Count() > 1);
    }
}
