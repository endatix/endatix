using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Data;
using Endatix.Core.Abstractions.Submitters;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Specifications;
using Endatix.Infrastructure.Identity.Authentication;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Infrastructure.Features.Submitters;

/// <summary>
/// Resolves a submitter from a context.
/// </summary>
internal sealed class SubmitterResolver(
    IRepository<Submitter> submitterRepository,
    IEnumerable<ISubmitterClaimExtractor> claimExtractors,
    SubmitterProfileSnapshotBuilder profileSnapshotBuilder,
    IDateTimeProvider dateTimeProvider,
    IUniqueConstraintViolationChecker uniqueConstraintViolationChecker) : ISubmitterResolver
{
    private static readonly SubmitterResolution _empty = new(null, null, null);

    /// <inheritdoc />
    public async Task<SubmitterResolution> FindExistingAsync(
        SubmitterResolveContext context,
        CancellationToken cancellationToken)
    {
        var input = GetExtractionInput(context);
        if (input is null)
        {
            return _empty;
        }

        var submitter = await FindExistingSubmitterAsync(context.TenantId, input, cancellationToken);
        return submitter is null
            ? _empty
            : new SubmitterResolution(submitter.Id, submitter.DisplayId, submitter.ProfileJson);
    }

    /// <inheritdoc />
    public async Task<SubmitterResolution> EnsureSubmitterAsync(
        SubmitterResolveContext context,
        CancellationToken cancellationToken)
    {
        var input = GetExtractionInput(context);
        if (input is null)
        {
            return _empty;
        }

        var draft = CreateDraft(context, input);
        var submitter = await FindExistingSubmitterAsync(context.TenantId, input, cancellationToken);
        if (submitter is null)
        {
            submitter = await CreateAsync(draft, cancellationToken);
        }
        else
        {
            await RefreshAsync(submitter, draft, cancellationToken);
        }

        return new SubmitterResolution(submitter.Id, submitter.DisplayId, draft.ProfileSnapshot);
    }

    private SubmitterDraft CreateDraft(SubmitterResolveContext context, SubmitterExtractionInput input) =>
        new(
            context.TenantId,
            input,
            FromTrustedPayload: context.Submitter is not null,
            profileSnapshotBuilder.Build(input.Profile),
            dateTimeProvider.UtcNow);

    /// <summary>
    /// Creates the Submitter. When a parallel request created it first, the unique index refuses this
    /// insert and the winner's row is refreshed and returned instead.
    /// </summary>
    private async Task<Submitter> CreateAsync(SubmitterDraft draft, CancellationToken cancellationToken)
    {
        var input = draft.Input;
        var submitter = Submitter.Create(
            draft.TenantId, input.AuthProvider, input.ExternalSubjectId, draft.LabelFor(null), input.AppUserId, draft.ProfileSnapshot, draft.Now);
        try
        {
            await submitterRepository.AddAsync(submitter, cancellationToken);
            return submitter;
        }
        catch (Exception exception) when (IsIdentityRace(exception))
        {
            ForgetFailedInsert(exception);
            var existing = await FindExistingSubmitterAsync(draft.TenantId, input, cancellationToken)
                ?? throw new InvalidOperationException("The Submitter that won the race could not be read back.", exception);
            await RefreshAsync(existing, draft, cancellationToken);
            return existing;
        }
    }

    private Task RefreshAsync(Submitter submitter, SubmitterDraft draft, CancellationToken cancellationToken)
    {
        submitter.Refresh(draft.LabelFor(submitter), draft.ProfileSnapshot, draft.Now);
        return submitterRepository.UpdateAsync(submitter, cancellationToken);
    }

    private bool IsIdentityRace(Exception exception)
    {
        var violation = uniqueConstraintViolationChecker.AnalyzeUniqueConstraint(exception);
        return violation.IsUniqueConstraintViolation && violation.IsSubmitterIdentityViolation();
    }

    /// <summary>
    /// A parallel request created this Submitter first. The losing insert stays tracked as Added, so
    /// the next save (the refresh below, then the submission itself) would repeat it and fail again.
    /// </summary>
    private static void ForgetFailedInsert(Exception exception)
    {
        if (exception is not DbUpdateException update)
        {
            return;
        }

        foreach (var entry in update.Entries)
        {
            entry.State = EntityState.Detached;
        }
    }

    private SubmitterExtractionInput? GetExtractionInput(SubmitterResolveContext context)
    {
        var input = context.Submitter is not null
            ? CreateInputFromTrustedPayload(context.Submitter)
            : ExtractFromPrincipal(context);

        if (input is null || input.AuthProvider == SubmitterAuthProviders.Anonymous)
        {
            return null;
        }

        return input;
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

    /// <summary>What one resolve writes: the identity read from the input, and when it was seen.</summary>
    private sealed record SubmitterDraft(
        long TenantId,
        SubmitterExtractionInput Input,
        bool FromTrustedPayload,
        string? ProfileSnapshot,
        DateTimeOffset Now)
    {
        /// <summary>
        /// The Label to store. A trusted payload without one keeps the stored Label, else takes the subject;
        /// a principal's Label is always what its claims give.
        /// </summary>
        public string? LabelFor(Submitter? existing) =>
            Input.DisplayId ?? (FromTrustedPayload ? existing?.DisplayId ?? Input.ExternalSubjectId : null);
    }
}
