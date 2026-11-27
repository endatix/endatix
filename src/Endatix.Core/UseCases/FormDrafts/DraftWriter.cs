using System.Text.Json;
using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Versioning;

namespace Endatix.Core.UseCases.FormDrafts;

internal sealed class DraftWriter(IFormDraftRepository drafts, ITenantContext tenant, IActorAccessor actor, IDateTimeProvider clock) :
    IQueryHandler<ReadDraft, Result<DraftResponse>>,
    ICommandHandler<ReplaceDraft, Result<DraftResponse>>,
    ICommandHandler<AppendDraft, Result<DraftResponse>>,
    ICommandHandler<DiscardDraft, Result<DraftResponse>>
{
    public async Task<Result<DraftResponse>> Handle(ReadDraft request, CancellationToken cancellationToken)
    {
        var state = await LoadAsync(request.FormId, cancellationToken);
        return state is null ? Result.NotFound("Form not found.") : await ResponseAsync(state, cancellationToken);
    }

    public Task<Result<DraftResponse>> Handle(ReplaceDraft request, CancellationToken cancellationToken) =>
        WriteAsync(request, cancellationToken);

    public Task<Result<DraftResponse>> Handle(AppendDraft request, CancellationToken cancellationToken) =>
        WriteAsync(request, cancellationToken);

    public Task<Result<DraftResponse>> Handle(DiscardDraft request, CancellationToken cancellationToken) =>
        WriteAsync(request, cancellationToken);

    private async Task<Result<DraftResponse>> WriteAsync(WriteDraft request, CancellationToken ct)
    {
        var state = await LoadAsync(request.FormId, ct);
        if (state is null)
        {
            return Result.NotFound("Form not found.");
        }
        if (request.ExpectedVersion != state.Head.Version)
        {
            return Result.Conflict("The draft changed since it was read.");
        }
        return await SaveAsync(request, state, ct);
    }

    private async Task<Result<DraftResponse>> SaveAsync(WriteDraft request, DraftState state, CancellationToken ct)
    {
        List<FormDraftOperation> rows;
        try
        {
            rows = await ChangeAsync(request, state, ct);
        }
        catch (JsonException)
        {
            return Result.Invalid(new ValidationError("Provide a survey JSON object without duplicate properties.") { Identifier = "jsonData" });
        }
        if (rows.Count != 0 && !await drafts.SaveOrConcedeAsync(state.Head, rows, ct))
        {
            return Result.Conflict("The draft changed while it was being saved.");
        }
        return await ResponseAsync(state, ct);
    }

    private async Task<List<FormDraftOperation>> ChangeAsync(WriteDraft request, DraftState state, CancellationToken ct)
    {
        var write = new DraftWrite(actor.Current, "api", clock.UtcNow.UtcDateTime);
        return request switch
        {
            ReplaceDraft replace => state.Head.Replace(replace.JsonData, write),
            DiscardDraft => state.Head.Discard(state.PublishedJson, write),
            AppendDraft append => state.Head.Append(append.Batch, write, await drafts.LatestSnapshotVersionAsync(request.FormId, ct)),
            _ => throw new ArgumentOutOfRangeException(nameof(request))
        };
    }

    private async Task<DraftState?> LoadAsync(long id, CancellationToken ct)
    {
        var form = tenant.TenantId > 0 ? await drafts.FindFormAsync(tenant.TenantId, id, ct) : null;
        if (form is null)
        {
            return null;
        }
        var published = form.ActiveDefinition?.JsonData ?? FormDefinitionDefaults.Json;
        var head = await drafts.FindHeadAsync(id, ct) ?? FormDraft.For(form, published);
        return new(head, form, published);
    }

    private async Task<DraftResponse> ResponseAsync(DraftState state, CancellationToken ct)
    {
        var head = state.Head;
        var lastActor = await drafts.LastActorAsync(head.FormId, head.Version, ct);
        return new(head.Version, head.JsonData, head.ContentHash, await drafts.ActiveNumberAsync(state.Form, ct),
            head.ContentHash != CanonicalSurveyJson.PublishedHash(state.PublishedJson), head.ModifiedAt, lastActor);
    }

    private sealed record DraftState(FormDraft Head, Form Form, string PublishedJson);
}
