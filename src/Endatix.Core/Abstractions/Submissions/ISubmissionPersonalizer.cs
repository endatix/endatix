using Endatix.Core.Abstractions.Submitters;

namespace Endatix.Core.Abstractions.Submissions;

/// <summary>
/// Audience values frozen onto a submission created for a known person.
/// The default does nothing. The audience module replaces it when personalization is on.
/// </summary>
public interface ISubmissionPersonalizer
{
    Task<PersonalizationFreeze?> FreezeAsync(
        SubmissionPersonalizationRequest request,
        CancellationToken cancellationToken);

    Task BindAsync(
        PersonalizationFreeze freeze,
        long submitterId,
        CancellationToken cancellationToken);
}

/// <summary>
/// Snapshot captured before the submission is saved, plus the person it belongs to and whether they are a test
/// participant on the form. A test participant on a form with no properties has no snapshot.
/// </summary>
public sealed record PersonalizationFreeze(long MemberId, string? Snapshot, bool IsTestParticipant);

/// <summary>Who the on-behalf call is for, and which form's audience to read.</summary>
public sealed record SubmissionPersonalizationRequest(
    long TenantId,
    long FormId,
    SubmitterInput? Submitter);
