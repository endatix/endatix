using System.Globalization;
using System.Text.Json;
using Endatix.Api.Infrastructure;
using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Modules.Reporting.Data;
using Endatix.Modules.Reporting.Domain;
using Endatix.Modules.Reporting.Features.SubmissionViews;
using FastEndpoints;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Modules.Reporting.Endpoints.Forms;

/// <summary>
/// Saves the submissions grid view everyone on a form sees.
/// </summary>
public sealed class SaveSubmissionView(
    IMediator mediator,
    ITenantContext tenantContext,
    IUserContext userContext)
    : Endpoint<SaveSubmissionViewRequest, Results<Ok<SubmissionViewResponse>, ProblemHttpResult>>
{
    /// <inheritdoc />
    public override void Configure()
    {
        Put("forms/{formId}/reporting/submission-view");
        Permissions(Actions.Forms.Edit);
        Summary(Describe);
        Description(builder => builder
            .Produces<SubmissionViewResponse>(200, "application/json")
            .ProducesProblem(400)
            .ProducesProblem(404)
            .ProducesProblem(409));
    }

    private static void Describe(EndpointSummary summary)
    {
        summary.Summary = "Save the form's submissions grid view";
        summary.Description =
            "Saves the grid view everyone on the form sees. Send the version the view was read at: 0 creates the " +
            "view, the current version replaces it. Any other version means someone saved in between, and " +
            "the save is refused with 409.";
        summary.ExampleRequest = new SaveSubmissionViewRequest
        {
            FormId = 1,
            Version = 2,
            Settings = SubmissionViewResponse.Example.Settings,
        };
        summary.ResponseExamples[200] = SubmissionViewResponse.Example;
        summary.Responses[200] = "Submissions grid view saved.";
        summary.Responses[400] = "Invalid input data, or settings that are malformed or too large.";
        summary.Responses[404] = "Form not found.";
        summary.Responses[409] = "Someone else saved the view after the given version.";
    }

    /// <inheritdoc />
    public override async Task<Results<Ok<SubmissionViewResponse>, ProblemHttpResult>> ExecuteAsync(
        SaveSubmissionViewRequest request,
        CancellationToken ct)
    {
        SubmissionViewSave save = new(
            tenantContext.TenantId,
            request.FormId,
            request.Version,
            request.Settings.GetRawText(),
            long.TryParse(userContext.GetCurrentUserId(), CultureInfo.InvariantCulture, out var userId) ? userId : null);

        var result = await mediator.Send(new SaveSubmissionViewCommand(save), ct);

        return TypedResultsBuilder
            .MapResult(result, SubmissionViewResponse.FromDto)
            .SetTypedResults<Ok<SubmissionViewResponse>, ProblemHttpResult>();
    }
}

/// <summary>Validates the request for the save submission view endpoint.</summary>
public sealed class SaveSubmissionViewValidator : Validator<SaveSubmissionViewRequest>
{
    /// <summary>Requires a form id and a version, and checks the settings' shape and size.</summary>
    public SaveSubmissionViewValidator()
    {
        RuleFor(request => request.FormId)
            .GreaterThan(0);

        RuleFor(request => request.Version)
            .GreaterThanOrEqualTo(SubmissionView.UnsavedVersion);

        RuleFor(request => request.Settings)
            .Must(settings => SettingsError(settings) is null)
            .WithMessage((_, settings) => SettingsError(settings));
    }

    private static string? SettingsError(JsonElement settings) =>
        settings.ValueKind == JsonValueKind.Undefined
            ? SubmissionViewSettings.Error(null)
            : SubmissionViewSettings.Error(settings.GetRawText());
}

/// <summary>The request for the save submission view endpoint.</summary>
public sealed class SaveSubmissionViewRequest
{
    /// <summary>The form whose view to save, from the route.</summary>
    public long FormId { get; init; }

    /// <summary>The version the view was read at; <c>0</c> when nobody had saved one.</summary>
    public long Version { get; init; }

    /// <summary><c>{"v":1,"columns":[{"key","visible","width"}],"pageSize","sort":{"key","direction"}}</c>.</summary>
    public JsonElement Settings { get; init; }
}
