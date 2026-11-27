using Endatix.Core.UseCases.FormDrafts;
using FastEndpoints;
using FluentValidation;
using MediatR;

namespace Endatix.Api.Endpoints.FormDrafts;

/// <summary>Replaces the survey in a draft with the supplied JSON.</summary>
[System.Diagnostics.CodeAnalysis.Experimental("ENDATIX0007")]
public sealed class ReplaceRequest : DraftRequest
{
    /// <summary>The full survey text.</summary>
    public string JsonData { get; set; } = "";
}

/// <summary>Checks the route and the replacement survey.</summary>
[System.Diagnostics.CodeAnalysis.Experimental("ENDATIX0007")]
public sealed class ReplaceValidator : Validator<ReplaceRequest>
{
    /// <summary>Requires a form ID and nonempty survey text.</summary>
    public ReplaceValidator()
    {
        Include(new DraftRequestValidator());
        RuleFor(x => x.JsonData).NotEmpty();
    }
}

/// <summary>Saves a whole-survey snapshot when the content changes.</summary>
[System.Diagnostics.CodeAnalysis.Experimental("ENDATIX0007")]
public sealed class Replace(IMediator mediator) : DraftWriteEndpoint<ReplaceRequest, DraftResponse>(mediator)
{
    /// <inheritdoc/>
    protected override Http Verb => Http.PUT;
    /// <inheritdoc/>
    protected override WriteDraft Command(ReplaceRequest request, long version) =>
        new ReplaceDraft(request.FormId, version, request.JsonData);
    /// <inheritdoc/>
    protected override DraftResponse Map(ReplaceRequest request, DraftResponse draft) => draft;
}
