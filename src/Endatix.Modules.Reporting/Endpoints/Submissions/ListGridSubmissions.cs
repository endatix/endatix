using Endatix.Api.Endpoints.Submissions;
using Endatix.Api.Infrastructure;
using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Infrastructure.Result;
using Endatix.Infrastructure.Features.Submitters;
using Endatix.Modules.Reporting.Features.SubmissionGrid;
using FastEndpoints;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;

namespace Endatix.Modules.Reporting.Endpoints.Submissions;

/// <summary>
/// Lists a form's submissions for the submissions grid, with their answers for the requested columns.
/// </summary>
public sealed class ListGridSubmissions(
    ListSubmissionGridRows listSubmissionGridRows,
    ITenantContext tenantContext)
    : Endpoint<ListGridSubmissionsRequest, Results<Ok<Paged<SubmissionGridRow>>, ProblemHttpResult>>
{
    /// <inheritdoc />
    public override void Configure()
    {
        Get("forms/{formId}/reporting/submissions");
        Permissions(Actions.Submissions.View);
        Summary(summary =>
        {
            summary.Summary = "List submissions for the submissions grid";
            summary.Description =
                "Takes the same filters, sort and paging as the form's submissions list, plus columns: a " +
                "comma-separated list of answer columns from the form's reporting schema. Each row carries its " +
                "preparation state and its answers for those columns, from prepared data when it is current and " +
                "from the submission's own answers otherwise.";
            summary.Responses[200] = "Submissions returned.";
            summary.Responses[400] = "Invalid input data.";
            summary.Responses[401] = "No tenant in the request.";
            summary.Responses[404] = "Form not found.";
        });
        Description(builder => builder
            .Produces<Paged<SubmissionGridRow>>(StatusCodes.Status200OK, "application/json")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound));
    }

    /// <inheritdoc />
    public override async Task<Results<Ok<Paged<SubmissionGridRow>>, ProblemHttpResult>> ExecuteAsync(
        ListGridSubmissionsRequest request,
        CancellationToken ct)
    {
        SubmissionGridRowsQuery query = new(
            tenantContext.TenantId,
            request.ToQuery(),
            SubmissionGridColumnKeys.Parse(request.Columns));
        var result = await listSubmissionGridRows.ExecuteAsync(query, ct);

        return TypedResultsBuilder
            .MapResult(result, rows => rows)
            .SetTypedResults<Ok<Paged<SubmissionGridRow>>, ProblemHttpResult>();
    }
}

/// <summary>
/// The submissions list request, plus the answer columns to return.
/// </summary>
public sealed class ListGridSubmissionsRequest : ListByFormIdRequest
{
    /// <summary>Comma-separated answer column keys. When omitted, rows carry no answers.</summary>
    [QueryParam]
    public string? Columns { get; init; }
}

public sealed class ListGridSubmissionsValidator : Validator<ListGridSubmissionsRequest>
{
    public ListGridSubmissionsValidator(IOptions<SubmitterOptions> submitterOptions)
    {
        Include(new ListByFormIdValidator(submitterOptions));

        RuleFor(request => request.Columns)
            .Must(columns => SubmissionGridColumnKeys.Parse(columns).Count <= SubmissionGridColumnKeys.MaxColumns)
            .WithMessage($"At most {SubmissionGridColumnKeys.MaxColumns} columns can be requested.")
            .Must(columns => SubmissionGridColumnKeys.Parse(columns).All(key => key.Length <= SubmissionGridColumnKeys.MaxKeyLength))
            .WithMessage($"A column key can be at most {SubmissionGridColumnKeys.MaxKeyLength} characters.");
    }
}
