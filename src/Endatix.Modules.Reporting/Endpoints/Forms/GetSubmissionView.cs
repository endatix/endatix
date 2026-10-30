using System.Text.Json;
using Endatix.Api.Infrastructure;
using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Modules.Reporting.Features.SubmissionViews;
using FastEndpoints;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Modules.Reporting.Endpoints.Forms;

/// <summary>
/// Returns the submissions grid view everyone on a form sees.
/// </summary>
public sealed class GetSubmissionView(
    IMediator mediator,
    ITenantContext tenantContext)
    : Endpoint<GetSubmissionViewRequest, Results<Ok<SubmissionViewResponse>, ProblemHttpResult>>
{
    /// <inheritdoc />
    public override void Configure()
    {
        Get("forms/{formId}/reporting/submission-view");
        Permissions(Actions.Submissions.View);
        Summary(Describe);
        Description(builder => builder
            .Produces<SubmissionViewResponse>(200, "application/json")
            .ProducesProblem(400)
            .ProducesProblem(404));
    }

    private static void Describe(EndpointSummary summary)
    {
        summary.Summary = "Get the form's submissions grid view";
        summary.Description =
            "Returns the grid view saved for everyone on the form, with the version to send back when saving it. " +
            "Columns the form no longer has are left out. When nobody saved a view, returns version 0 and the " +
            "default settings.";
        summary.ResponseExamples[200] = SubmissionViewResponse.Example;
        summary.Responses[200] = "Submissions grid view returned.";
        summary.Responses[400] = "Invalid input data.";
        summary.Responses[404] = "Form not found.";
    }

    /// <inheritdoc />
    public override async Task<Results<Ok<SubmissionViewResponse>, ProblemHttpResult>> ExecuteAsync(
        GetSubmissionViewRequest request,
        CancellationToken ct)
    {
        var result = await mediator.Send(
            new GetSubmissionViewQuery(tenantContext.TenantId, request.FormId),
            ct);

        return TypedResultsBuilder
            .MapResult(result, SubmissionViewResponse.FromDto)
            .SetTypedResults<Ok<SubmissionViewResponse>, ProblemHttpResult>();
    }
}

/// <summary>Validates the request for the get submission view endpoint.</summary>
public sealed class GetSubmissionViewValidator : Validator<GetSubmissionViewRequest>
{
    /// <summary>Requires a form id.</summary>
    public GetSubmissionViewValidator()
    {
        RuleFor(request => request.FormId)
            .GreaterThan(0);
    }
}

/// <summary>The request for the get submission view endpoint.</summary>
public sealed class GetSubmissionViewRequest
{
    /// <summary>The form whose view to read, from the route.</summary>
    public long FormId { get; init; }
}

/// <summary>A form's submissions grid view; also the response of a save.</summary>
public sealed class SubmissionViewResponse
{
    internal static readonly SubmissionViewResponse Example = FromDto(new SubmissionViewDto(
        3,
        """{"v":1,"columns":[{"key":"q1","visible":true,"width":240}],"pageSize":25,"sort":{"key":"createdAt","direction":"desc"}}"""));

    /// <summary>The version to send back with the next save; <c>0</c> when nobody saved a view.</summary>
    public required long Version { get; init; }

    /// <summary><c>{"v":1,"columns":[{"key","visible","width"}],"pageSize","sort":{"key","direction"}}</c>.</summary>
    public required JsonElement Settings { get; init; }

    internal static SubmissionViewResponse FromDto(SubmissionViewDto dto)
    {
        using var settings = JsonDocument.Parse(dto.SettingsJson);
        return new SubmissionViewResponse
        {
            Version = dto.Version,
            Settings = settings.RootElement.Clone(),
        };
    }
}
