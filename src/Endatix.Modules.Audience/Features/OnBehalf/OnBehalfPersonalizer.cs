using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Submissions;
using Endatix.Core.Abstractions.Submitters;
using Endatix.Modules.Audience.Domain;
using Endatix.Modules.Audience.Features.Links;
using Endatix.Modules.Audience.Features.Settings;
using Endatix.Modules.Audience.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Endatix.Modules.Audience.Features.OnBehalf;

internal sealed class OnBehalfPersonalizer(
    IAudienceDbContext db,
    IValueNormalizer normalizer,
    ILogger<OnBehalfPersonalizer> logger) : ISubmissionPersonalizer
{
    public async Task<PersonalizationFreeze?> FreezeAsync(
        SubmissionPersonalizationRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Submitter is not SubmitterInput submitter
            || !CanFreeze(submitter)
            || !await HasPropertiesAsync(request, cancellationToken))
        {
            return null;
        }

        return await FreezeMemberAsync(request, submitter, cancellationToken);
    }

    /// <summary>
    /// Binds the member to the submission's Submitter once. The update only matches a member with no
    /// Submitter yet, so parallel creates cannot re-point it. Best effort: the submission is already
    /// saved, and failing here would make the caller retry into a 409 for a submission that exists.
    /// </summary>
    public async Task BindAsync(
        PersonalizationFreeze freeze,
        long submitterId,
        CancellationToken cancellationToken)
    {
        try
        {
            await db.BindSubmitterOnceAsync(new SubmitterBinding(freeze.MemberId, submitterId), cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(
                ex,
                "Could not bind audience member {MemberId} to submitter {SubmitterId}",
                freeze.MemberId,
                submitterId);
        }
    }

    private async Task<PersonalizationFreeze?> FreezeMemberAsync(
        SubmissionPersonalizationRequest request,
        SubmitterInput submitter,
        CancellationToken cancellationToken)
    {
        var subjectId = submitter.ExternalSubjectId;
        var member = await FindMemberAsync(request.TenantId, subjectId, cancellationToken);
        var membershipId = member is null
            ? null
            : await MembershipIdAsync(member, request.FormId, cancellationToken);
        if (member is null || membershipId is null)
        {
            return Unknown(subjectId, request.FormId);
        }

        var snapshot = await SnapshotAsync(member, membershipId.Value, cancellationToken);
        return new PersonalizationFreeze(member.Id, snapshot);
    }

    private static bool CanFreeze(SubmitterInput submitter) =>
        !string.IsNullOrWhiteSpace(submitter.ExternalSubjectId);

    private Task<bool> HasPropertiesAsync(
        SubmissionPersonalizationRequest request,
        CancellationToken cancellationToken) =>
        db.Properties.AnyAsync(
            property => property.TenantId == request.TenantId && property.FormId == request.FormId,
            cancellationToken);

    private PersonalizationFreeze? Unknown(string businessId, long formId)
    {
        // Debug, not Information: a BusinessId can be personal data, and production keeps Information.
        logger.LogDebug(
            "Unknown BusinessId {BusinessId} on form {FormId}",
            businessId,
            formId);
        return null;
    }

    private async Task<Member?> FindMemberAsync(
        long tenantId,
        string externalSubjectId,
        CancellationToken cancellationToken)
    {
        var kind = await IdentifierKindReader.GetAsync(db, tenantId, cancellationToken);
        var normalized = Member.Normalize(externalSubjectId, kind, normalizer);
        return await db.Members.AsNoTracking().FirstOrDefaultAsync(
            person => person.TenantId == tenantId && person.NormalizedIdentifier == normalized,
            cancellationToken);
    }

    private Task<long?> MembershipIdAsync(
        Member member,
        long formId,
        CancellationToken cancellationToken) =>
        db.Memberships.AsNoTracking()
            .Where(membership => membership.TenantId == member.TenantId
                && membership.FormId == formId
                && membership.MemberId == member.Id)
            .Select(membership => (long?)membership.Id)
            .FirstOrDefaultAsync(cancellationToken);

    private async Task<string> SnapshotAsync(
        Member member,
        long membershipId,
        CancellationToken cancellationToken)
    {
        var cells = await SnapshotCells.LoadAsync(
            db,
            new SnapshotMembership(member.TenantId, membershipId),
            cancellationToken);
        return PersonalizationSnapshotJson.Write(
            new SnapshotHeader(null, member.Identifier, DateTime.UtcNow, PersonalizationSources.OnBehalf, member.Id),
            cells);
    }

}
