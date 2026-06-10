using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Submitters;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Specifications;
using Endatix.Infrastructure.Identity.Authentication;

namespace Endatix.Infrastructure.Features.Submitters;

/// <summary>
/// Resolves a submitter from a context.
/// </summary>
internal sealed class SubmitterResolver(
    IRepository<Submitter> submitterRepository,
    IEnumerable<ISubmitterClaimExtractor> claimExtractors,
    SubmitterProfileSnapshotBuilder profileSnapshotBuilder,
    IDateTimeProvider dateTimeProvider) : ISubmitterResolver
{
    private static readonly SubmitterResolution _empty = new(null, null, null);

    /// <inheritdoc />
    public async Task<SubmitterResolution> ResolveAsync(
        SubmitterResolveContext context,
        CancellationToken cancellationToken)
    {
        var input = context.Submitter is not null
            ? CreateInputFromTrustedPayload(context.Submitter)
            : ExtractFromPrincipal(context);

        if (input is null || input.AuthProvider == SubmitterAuthProviders.Anonymous)
        {
            return _empty;
        }

        var profileSnapshot = profileSnapshotBuilder.Build(input.Profile);
        var submitter = await FindExistingSubmitterAsync(context.TenantId, input, cancellationToken);
        var now = dateTimeProvider.UtcNow;

        if (submitter is null)
        {
            submitter = Submitter.Create(
                context.TenantId,
                input.AuthProvider,
                input.ExternalSubjectId,
                input.DisplayId,
                input.AppUserId,
                profileSnapshot,
                now);

            await submitterRepository.AddAsync(submitter, cancellationToken);
        }
        else
        {
            submitter.Refresh(input.DisplayId, profileSnapshot, now);
            await submitterRepository.UpdateAsync(submitter, cancellationToken);
        }

        return new SubmitterResolution(
            submitter.Id,
            submitter.DisplayId,
            profileSnapshot);
    }

    private SubmitterExtractionInput? ExtractFromPrincipal(SubmitterResolveContext context)
    {
        if (context.Principal is null)
        {
            return null;
        }

        var extractor = claimExtractors
            .OrderBy(extractor => extractor.Priority)
            .FirstOrDefault(extractor => extractor.CanExtract(context.Principal));

        return extractor?.Extract(context.Principal);
    }

    private static SubmitterExtractionInput CreateInputFromTrustedPayload(SubmitterInput input) =>
        new(
            Normalize(input.AuthProvider) ?? SubmitterAuthProviders.Integration,
            Normalize(input.ExternalSubjectId),
            Normalize(input.DisplayId),
            input.AppUserId,
            input.Profile);

    private async Task<Submitter?> FindExistingSubmitterAsync(
        long tenantId,
        SubmitterExtractionInput input,
        CancellationToken cancellationToken)
    {
        if (input.AppUserId is not null && input.AuthProvider == AuthProviders.Endatix)
        {
            return await submitterRepository.SingleOrDefaultAsync(
                new SubmitterSpecifications.ByAppUserSpec(tenantId, input.AuthProvider, input.AppUserId.Value),
                cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(input.ExternalSubjectId))
        {
            return await submitterRepository.SingleOrDefaultAsync(
                new SubmitterSpecifications.ByExternalSubjectSpec(tenantId, input.AuthProvider, input.ExternalSubjectId),
                cancellationToken);
        }

        return null;
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
