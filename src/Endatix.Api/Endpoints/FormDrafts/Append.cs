using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Endatix.Core.Entities;
using Endatix.Core.UseCases.FormDrafts;
using FastEndpoints;
using FluentValidation;
using MediatR;

namespace Endatix.Api.Endpoints.FormDrafts;

/// <summary>Journal emissions from one editor save and the resulting survey.</summary>
[Experimental("ENDATIX0007")]
public sealed class AppendRequest : DraftRequest
{
    /// <summary>The client's save sequence, echoed in the response.</summary>
    public int SaveNo { get; set; }
    /// <summary>Client-supplied editing session identifier.</summary>
    public string ClientSessionId { get; set; } = "";
    /// <summary>Journal emissions in their original order, including repeated sequence numbers.</summary>
    public JsonElement[] Records { get; set; } = [];
    /// <summary>The full JSON after applying every edit in this batch.</summary>
    public string JsonData { get; set; } = "";
    /// <summary>The writer's hash, stored on the final journal row for replay verification.</summary>
    public string ContentHash { get; set; } = "";
}

/// <summary>Validates the batch envelope; the server does not replay the records.</summary>
[Experimental("ENDATIX0007")]
public sealed class AppendValidator : Validator<AppendRequest>
{
    /// <summary>Requires journal records and the resulting survey.</summary>
    public AppendValidator()
    {
        Include(new DraftRequestValidator());
        RuleFor(x => x.JsonData).NotEmpty();
        RuleFor(x => x.Records).NotEmpty();
        RuleForEach(x => x.Records).Must(record => record.ValueKind == JsonValueKind.Object);
        RuleFor(x => x.ClientSessionId).NotEmpty();
        RuleFor(x => x.ContentHash).Matches("^[a-fA-F0-9]{64}$").NotEmpty();
        RuleFor(x => x.SaveNo).GreaterThanOrEqualTo(0);
    }
}

/// <summary>Acknowledges a save with its new version and server-computed hash.</summary>
[Experimental("ENDATIX0007")]
public sealed record AppendResponse(long Version, string ContentHash, int SaveNo);

/// <summary>Appends an autosave to a draft's journal.</summary>
[Experimental("ENDATIX0007")]
public sealed class Append(IMediator mediator) : DraftWriteEndpoint<AppendRequest, AppendResponse>(mediator)
{
    /// <inheritdoc/>
    protected override Http Verb => Http.PATCH;
    /// <inheritdoc/>
    protected override WriteDraft Command(AppendRequest request, long version) =>
        new AppendDraft(request.FormId, version, new DraftJournalBatch(request.SaveNo, request.ClientSessionId,
            request.Records.Select(record => record.GetRawText()).ToArray(), request.JsonData, request.ContentHash));
    /// <inheritdoc/>
    protected override AppendResponse Map(AppendRequest request, DraftResponse draft) =>
        new(draft.Version, draft.ContentHash, request.SaveNo);
}
