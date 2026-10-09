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
        if (!CanFreeze(request.Submitter) || !await HasPropertiesAsync(request.FormId, cancellationToken))
        {
            return null;
        }

        return await FreezeMemberAsync(request, cancellationToken);
    }

    public async Task BindAsync(
        PersonalizationFreeze freeze,
        long submitterId,
        CancellationToken cancellationToken)
    {
        Member? member = await db.Members
            .FirstOrDefaultAsync(person => person.Id == freeze.MemberId, cancellationToken);
        if (member is null || member.SubmitterId is not null)
        {
            return;
        }

        member.BindSubmitter(submitterId);
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<PersonalizationFreeze?> FreezeMemberAsync(
        SubmissionPersonalizationRequest request,
        CancellationToken cancellationToken)
    {
        string subjectId = request.Submitter!.ExternalSubjectId;
        Member? member = await FindMemberAsync(request.TenantId, subjectId, cancellationToken);
        long? membershipId = member is null
            ? null
            : await MembershipIdAsync(request.FormId, member.Id, cancellationToken);
        if (member is null || membershipId is null)
        {
            return Unknown(subjectId, request.FormId);
        }

        string snapshot = await SnapshotAsync(member, membershipId.Value, cancellationToken);
        return new PersonalizationFreeze(member.Id, snapshot);
    }

    private static bool CanFreeze(SubmitterInput? submitter) =>
        submitter is not null && !string.IsNullOrWhiteSpace(submitter.ExternalSubjectId);

    private Task<bool> HasPropertiesAsync(long formId, CancellationToken cancellationToken) =>
        db.Properties.AnyAsync(property => property.FormId == formId, cancellationToken);

    private PersonalizationFreeze? Unknown(string businessId, long formId)
    {
        logger.LogInformation(
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
        string kind = await IdentifierKindReader.GetAsync(db, tenantId, cancellationToken);
        string normalized = Member.Normalize(externalSubjectId, kind, normalizer);
        return await db.Members.AsNoTracking().FirstOrDefaultAsync(
            person => person.TenantId == tenantId && person.NormalizedIdentifier == normalized,
            cancellationToken);
    }

    private Task<long?> MembershipIdAsync(
        long formId,
        long memberId,
        CancellationToken cancellationToken) =>
        db.Memberships.AsNoTracking()
            .Where(membership => membership.FormId == formId && membership.MemberId == memberId)
            .Select(membership => (long?)membership.Id)
            .FirstOrDefaultAsync(cancellationToken);

    private async Task<string> SnapshotAsync(
        Member member,
        long membershipId,
        CancellationToken cancellationToken)
    {
        List<SnapshotCell> cells = await LoadCellsAsync(membershipId, cancellationToken);
        return PersonalizationSnapshotJson.Write(
            new SnapshotHeader(null, member.Identifier, DateTime.UtcNow, "on_behalf", member.Id),
            cells);
    }

    private async Task<List<SnapshotCell>> LoadCellsAsync(
        long membershipId,
        CancellationToken cancellationToken)
    {
        var rows = await (
            from value in db.PropertyValues
            join property in db.Properties on value.PropertyId equals property.Id
            where value.MembershipId == membershipId
            select new { property.VariableName, property.DataType, value.Value })
            .ToListAsync(cancellationToken);
        return rows
            .Select(row => new SnapshotCell(row.VariableName, row.DataType, row.Value))
            .ToList();
    }
}
