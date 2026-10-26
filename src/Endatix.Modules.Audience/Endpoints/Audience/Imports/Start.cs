using Endatix.Api.Infrastructure;
using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Common;
using Endatix.Modules.Audience.Domain;
using Endatix.Modules.Audience.Features.Imports;
using FastEndpoints;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Modules.Audience.Endpoints.Audience.Imports;

/// <summary>
/// Starts a staged Participant import: the browser then sends the file's rows in chunks.
/// </summary>
public sealed class Start(
    IMediator mediator,
    ITenantContext tenantContext,
    IUserContext userContext)
    : Endpoint<StartAudienceImportRequest, Results<Created<AudienceImportResponse>, ProblemHttpResult>>
{
    /// <inheritdoc />
    public override void Configure()
    {
        Post("forms/{formId}/audience/imports");
        Permissions(Actions.Forms.Edit);
        Summary(summary =>
        {
            summary.Summary = "Start a Participant import";
            summary.Description = "Starts an import of a CSV the browser has parsed: its header row, its row count, the " +
                "column mapping and the emptyCells and missingProperties options, fixed from here on. Send the rows with " +
                "PUT …/chunks/{index}; the last chunk starts the Check, which writes nothing. One unfinished import a form.";
            summary.Responses[201] = "Import started; send its chunks.";
            summary.Responses[400] = "Too many rows, no identifier column, a mapping that does not fit, or a bad option.";
            summary.Responses[404] = "Form not found.";
            summary.Responses[409] = "The form already has an unfinished import; its id is in the problem's existing resource.";
        });
        Description(builder => builder
            .Produces<AudienceImportResponse>(StatusCodes.Status201Created, "application/json")
            .ProducesProblem(400)
            .ProducesProblem(404)
            .ProducesProblem(409));
    }

    /// <inheritdoc />
    public override async Task<Results<Created<AudienceImportResponse>, ProblemHttpResult>> ExecuteAsync(
        StartAudienceImportRequest request,
        CancellationToken ct)
    {
        var result = await mediator.Send(ToCommand(request), ct);

        return TypedResultsBuilder
            .MapResult(result, AudienceImportResponse.FromDto)
            .SetTypedResults<Created<AudienceImportResponse>, ProblemHttpResult>();
    }

    private StartImportCommand ToCommand(StartAudienceImportRequest request) =>
        new(
            tenantContext.TenantId,
            request.FormId,
            request.FileName!,
            request.Headers ?? [],
            request.RowCount,
            request.IdentifierColumn!,
            request.PropertyColumns ?? [],
            request.IsTest,
            long.TryParse(userContext.GetCurrentUserId(), out long userId) ? userId : null,
            new AudienceImportOptions(
                request.EmptyCells == AudienceImportOptionCodes.Clear,
                request.MissingProperties == AudienceImportOptionCodes.Clear));
}

/// <summary>
/// Validator for starting a Participant import.
/// </summary>
public sealed class StartAudienceImportValidator : Validator<StartAudienceImportRequest>
{
    /// <summary>Most columns a file may have.</summary>
    public const int MaxColumns = 500;

    /// <summary>Builds the rules.</summary>
    public StartAudienceImportValidator()
    {
        RuleFor(request => request.FormId).GreaterThan(0);
        RuleFor(request => request.FileName).NotEmpty().MaximumLength(DataSchemaConstants.MAX_NAME_LENGTH);
        RuleFor(request => request.Headers).NotEmpty();
        RuleFor(request => request.Headers!.Count).LessThanOrEqualTo(MaxColumns).When(request => request.Headers is not null);
        RuleForEach(request => request.Headers).MaximumLength(DataSchemaConstants.MAX_NAME_LENGTH);
        RuleFor(request => request.RowCount).GreaterThan(0);
        RuleFor(request => request.IdentifierColumn).NotEmpty();
        RuleFor(request => request.EmptyCells).Must(BeAnOption).WithMessage(OptionMessage);
        RuleFor(request => request.MissingProperties).Must(BeAnOption).WithMessage(OptionMessage);
    }

    private const string OptionMessage = "'{PropertyName}' must be keep or clear.";

    private static bool BeAnOption(string? code) => code is null || AudienceImportOptionCodes.All.Contains(code);
}

/// <summary>
/// Request to start a Participant import.
/// </summary>
public sealed class StartAudienceImportRequest
{
    /// <summary>The form.</summary>
    public long FormId { get; init; }

    /// <summary>The file name the scripter chose.</summary>
    public string? FileName { get; init; }

    /// <summary>The file's header row, in order; each staged row's cells follow it.</summary>
    public List<string>? Headers { get; init; }

    /// <summary>Data rows in the file, not counting the header row.</summary>
    public int RowCount { get; init; }

    /// <summary>The header of the column that holds each row's identifier.</summary>
    public string? IdentifierColumn { get; init; }

    /// <summary>Maps property <c>variableName</c> → CSV header for simple columns.</summary>
    public Dictionary<string, string>? PropertyColumns { get; init; }

    /// <summary>Adds the people new to this form as test persons; people already on it get no writes.</summary>
    public bool IsTest { get; init; }

    /// <summary>
    /// <c>keep</c> (default) or <c>clear</c>: what an empty cell, or a choice Property with no ticked column, does to
    /// a stored value.
    /// </summary>
    public string? EmptyCells { get; init; }

    /// <summary>
    /// <c>keep</c> (default) or <c>clear</c>: what the write does to the stored values of the form's Properties the
    /// file has no column for, for the people in the file. The identifier's Property is never cleared.
    /// </summary>
    public string? MissingProperties { get; init; }
}
