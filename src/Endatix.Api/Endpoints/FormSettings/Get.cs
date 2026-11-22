using System.Diagnostics.CodeAnalysis;
using Endatix.Api.Common;
using Endatix.Api.Infrastructure;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.UseCases.FormSettings.Get;
using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Api.Endpoints.FormSettings;

/// <summary>Reads a form's settings: every section, with defaults where nothing is stored.</summary>
[Experimental("ENDATIX0005")]
public sealed class Get(IMediator mediator) : Endpoint<GetFormSettingsRequest, Results<Ok<FormSettingsModel>, ProblemHttpResult>>
{
    /// <inheritdoc/>
    public override void Configure()
    {
        Get("forms/{formId}/settings");
        Permissions(Actions.Forms.View);
        Summary(s =>
        {
            s.Summary = "Get a form's settings";
            s.Description = "Returns every section of the form's settings, with defaults where nothing is stored, and the " +
                "version. The ETag header holds the version a write names in If-Match: \"0\" before the first write.";
            s.Responses[200] = "The form's settings.";
            s.Responses[404] = "Form not found.";
        });
        Description(builder => builder
            .Produces<FormSettingsModel>(StatusCodes.Status200OK, "application/json")
            .ProducesProblem(StatusCodes.Status404NotFound));
    }

    /// <inheritdoc/>
    public override async Task<Results<Ok<FormSettingsModel>, ProblemHttpResult>> ExecuteAsync(
        GetFormSettingsRequest req,
        CancellationToken ct)
    {
        var result = await mediator.Send(new GetFormSettingsQuery(req.FormId), ct);
        if (!result.IsSuccess)
        {
            return result.ToProblem();
        }

        HttpContext.Response.Headers.ETag = EntityTagHeader.Format(result.Value.Version);
        return TypedResults.Ok(FormSettingsModel.Map(result.Value));
    }
}

/// <summary>Request to read a form's settings.</summary>
[Experimental("ENDATIX0005")]
public sealed class GetFormSettingsRequest
{
    /// <summary>The form.</summary>
    public long FormId { get; set; }
}
