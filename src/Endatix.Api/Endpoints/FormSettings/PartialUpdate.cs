using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Endatix.Api.Common;
using Endatix.Api.Infrastructure;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.FormSettings;
using Endatix.Core.UseCases.FormSettings.Get;
using Endatix.Core.UseCases.FormSettings.PartialUpdate;
using FastEndpoints;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Api.Endpoints.FormSettings;

/// <summary>
/// Replaces the sections of a form's settings that the body names, each whole. It names the version it read in
/// <c>If-Match</c> (ADR 0002): 428 when that is missing, 400 when it is not one version, 412 with the current version
/// when it is stale. A body that matches what is stored changes nothing, whatever <c>If-Match</c> names.
/// </summary>
[Experimental("ENDATIX0005")]
public sealed class PartialUpdate(IMediator mediator)
    : Endpoint<PartialUpdateFormSettingsRequest, Results<Ok<FormSettingsModel>, ProblemHttpResult>>
{
    /// <inheritdoc/>
    public override void Configure()
    {
        Patch("forms/{formId}/settings");
        Permissions(Actions.Forms.Edit);
        Summary(s =>
        {
            s.Summary = "Change a form's settings";
            s.Description = "Replaces each section the body names, whole; sections it leaves out keep their value. " +
                "If-Match names the version read from GET (\"0\" before the first write). Sending the stored values " +
                "again changes nothing.";
            s.Responses[200] = "The form's settings after the write. The ETag header holds the new version.";
            s.Responses[400] = "Invalid request or If-Match, or the body names no section.";
            s.Responses[404] = "Form not found.";
            s.Responses[412] = "If-Match names a version that is no longer current; the ETag header and version hold the current one.";
            s.Responses[428] = "If-Match is missing.";
        });
        Description(builder => builder
            .Produces<FormSettingsModel>(StatusCodes.Status200OK, "application/json")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status412PreconditionFailed)
            .ProducesProblem(StatusCodes.Status428PreconditionRequired));
    }

    /// <inheritdoc/>
    public override async Task<Results<Ok<FormSettingsModel>, ProblemHttpResult>> ExecuteAsync(
        PartialUpdateFormSettingsRequest req,
        CancellationToken ct)
    {
        // Missing is 428 here, not a validator's 400.
        if (EntityTagHeader.ReadIfMatch(HttpContext.Request.Headers.IfMatch.ToString(), out var expectedVersion) is { } refused)
        {
            return refused;
        }

        var change = new FormSettingsChange { Pdf = req.Pdf?.ToSection() };
        var result = await mediator.Send(new PartialUpdateFormSettingsCommand(req.FormId, expectedVersion, change), ct);
        return result.Status == ResultStatus.Conflict
            ? await StaleAsync(req.FormId, result, ct)
            : Saved(result);
    }

    private Results<Ok<FormSettingsModel>, ProblemHttpResult> Saved(Result<FormSettingsDto> result)
    {
        if (!result.IsSuccess)
        {
            return result.ToProblem();
        }

        EntityTagHeader.SetETag(HttpContext.Response, result.Value.Version);
        return TypedResults.Ok(FormSettingsModel.Map(result.Value));
    }

    // ADR 0002: a stale write returns the current version, so the client can retry without reading first. Every
    // conflict here is a stale version or a lost race; the shared ToProblem keeps a conflict as 409.
    private async Task<ProblemHttpResult> StaleAsync(long formId, Result<FormSettingsDto> result, CancellationToken ct)
    {
        var problem = TypedResults.Problem(EndatixProblemDetails.Create(
            StatusCodes.Status412PreconditionFailed, "Precondition failed", string.Join('\n', result.Errors)));
        var current = await mediator.Send(new GetFormSettingsQuery(formId), ct);
        if (current.IsSuccess)
        {
            EntityTagHeader.SetETag(HttpContext.Response, current.Value.Version);
            problem.ProblemDetails.Extensions["version"] = current.Value.Version.ToString(CultureInfo.InvariantCulture);
        }

        return problem;
    }
}

/// <summary>Request to change a form's settings: the sections to replace. A section left out keeps its value.</summary>
[Experimental("ENDATIX0005")]
public sealed class PartialUpdateFormSettingsRequest
{
    /// <summary>The form.</summary>
    public long FormId { get; set; }

    /// <summary>The PDF section, replaced whole.</summary>
    public PdfSettingsModel? Pdf { get; set; }
}

/// <summary>Validation rules for <see cref="PartialUpdateFormSettingsRequest"/>.</summary>
[Experimental("ENDATIX0005")]
public sealed class PartialUpdateFormSettingsValidator : Validator<PartialUpdateFormSettingsRequest>
{
    /// <summary>The form id is positive and the body names at least one section.</summary>
    public PartialUpdateFormSettingsValidator()
    {
        RuleFor(x => x.FormId).GreaterThan(0);
        RuleFor(x => x.Pdf).NotNull().WithMessage("Name at least one section, such as pdf.");
    }
}
