using Endatix.Core.UseCases.FormDrafts;
using FastEndpoints;
using MediatR;

namespace Endatix.Api.Endpoints.FormDrafts;

/// <summary>Restores the live survey to the draft while retaining the edit history.</summary>
[System.Diagnostics.CodeAnalysis.Experimental("ENDATIX0007")]
public sealed class Discard(IMediator mediator) : DraftWriteEndpoint<DraftRequest, DraftResponse>(mediator)
{
    /// <inheritdoc/>
    protected override Http Verb => Http.DELETE;
    /// <inheritdoc/>
    protected override WriteDraft Command(DraftRequest request, long version) => new DiscardDraft(request.FormId, version);
    /// <inheritdoc/>
    protected override DraftResponse Map(DraftRequest request, DraftResponse draft) => draft;
}
