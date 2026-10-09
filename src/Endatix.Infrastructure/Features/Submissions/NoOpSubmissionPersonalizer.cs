using Endatix.Core.Abstractions.Submissions;

namespace Endatix.Infrastructure.Features.Submissions;

internal sealed class NoOpSubmissionPersonalizer : ISubmissionPersonalizer
{
    public Task<PersonalizationFreeze?> FreezeAsync(
        SubmissionPersonalizationRequest request,
        CancellationToken cancellationToken) =>
        Task.FromResult<PersonalizationFreeze?>(null);

    public Task BindAsync(
        PersonalizationFreeze freeze,
        long submitterId,
        CancellationToken cancellationToken) =>
        Task.CompletedTask;
}
