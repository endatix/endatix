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
    : Endpoint<ListGridSubmissionsRequest, Results<Ok<SubmissionGridPage>, ProblemHttpResult>>
{
    private const string ListDescription =
        "Takes the same filters, sort and paging as the form's submissions list, plus columns: a comma-separated " +
        "list of answer columns from the form's reporting schema. Each row carries its preparation state and its " +
        "answers for those columns, from prepared data when it is current and from the submission's own answers " +
        "otherwise. The submission's own answers have simple question columns only. Repeated " +
        "answer={operator}:{key}:{value} parameters filter by prepared answers, AND-combined: " +
        "contains on text columns, from and to (yyyy-MM-dd) on date columns. The key is percent-encoded. Only rows " +
        "prepared from the submission's current version are checked; answerFilterCoverage counts the others.";

    /// <inheritdoc />
    public override void Configure()
    {
        Get("forms/{formId}/reporting/submissions");
        Permissions(Actions.Submissions.View);
        Summary(summary =>
        {
            summary.Summary = "List submissions for the submissions grid";
            summary.Description = ListDescription;
            summary.Responses[200] = "Submissions returned.";
            summary.Responses[400] = "Invalid input data.";
            summary.Responses[401] = "No tenant in the request.";
            summary.Responses[404] = "Form not found.";
            summary.Responses[409] = "Answer filters on a form with no prepared reporting schema (reporting_not_prepared).";
        });
        Description(builder => builder
            .Produces<SubmissionGridPage>(StatusCodes.Status200OK, "application/json")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict));
    }

    /// <inheritdoc />
    public override async Task<Results<Ok<SubmissionGridPage>, ProblemHttpResult>> ExecuteAsync(
        ListGridSubmissionsRequest request,
        CancellationToken ct)
    {
        var result = await listSubmissionGridRows.ExecuteAsync(ToRowsQuery(request), ct);
        if (result.Status is ResultStatus.Conflict)
        {
            return NotPrepared();
        }

        return TypedResultsBuilder
            .MapResult(result, rows => rows)
            .SetTypedResults<Ok<SubmissionGridPage>, ProblemHttpResult>();
    }

    private SubmissionGridRowsQuery ToRowsQuery(ListGridSubmissionsRequest request) =>
        new(
            tenantContext.TenantId,
            request.ToQuery(),
            SubmissionGridColumnKeys.Parse(request.Columns),
            AnswerFilterExpressions.TryParse(request.Answer) ?? []);

    // The only conflict the list returns: answer filters on a form that has no prepared schema yet.
    private static ProblemHttpResult NotPrepared() =>
        TypedResults.Problem(EndatixProblemDetails.Create(
            StatusCodes.Status409Conflict,
            title: null,
            detail: SubmissionGridAnswerSearch.NotPreparedMessage,
            errorCode: SubmissionGridAnswerSearch.NotPreparedErrorCode));
}

/// <summary>
/// The submissions list request, plus the answer columns to return and the answer filters.
/// </summary>
public sealed class ListGridSubmissionsRequest : ListByFormIdRequest
{
    /// <summary>Comma-separated answer column keys. When omitted, rows carry no answers.</summary>
    [QueryParam]
    public string? Columns { get; init; }

    /// <summary>Answer filters, each <c>{operator}:{percent-encoded key}:{value}</c>; AND-combined.</summary>
    [QueryParam]
    public IEnumerable<string>? Answer { get; init; }
}

/// <summary>
/// Validates the request for the list grid submissions endpoint.
/// </summary>
public sealed class ListGridSubmissionsValidator : Validator<ListGridSubmissionsRequest>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ListGridSubmissionsValidator"/> class.
    /// </summary>
    /// <param name="submitterOptions">The submitter options the submissions list validates its filters with.</param>
    public ListGridSubmissionsValidator(IOptions<SubmitterOptions> submitterOptions)
    {
        Include(new ListByFormIdValidator(submitterOptions));

        RuleFor(request => request.Columns)
            .Must(columns => SubmissionGridColumnKeys.Parse(columns).Count <= SubmissionGridColumnKeys.MaxColumns)
            .WithMessage($"At most {SubmissionGridColumnKeys.MaxColumns} columns can be requested.")
            .Must(columns => SubmissionGridColumnKeys.Parse(columns).All(key => key.Length <= SubmissionGridColumnKeys.MaxKeyLength))
            .WithMessage($"A column key can be at most {SubmissionGridColumnKeys.MaxKeyLength} characters.");

        RuleFor(request => request.Answer)
            .Must(answers => AnswerFilterExpressions.TryParse(answers) is not null)
            .WithMessage(
                "Each answer filter must be contains:{key}:{text} (1 to " +
                $"{AnswerFilterExpressions.MaxTextLength} characters), from:{{key}}:{{yyyy-MM-dd}} or to:{{key}}:{{yyyy-MM-dd}}.")
            .Must(answers => (AnswerFilterExpressions.TryParse(answers)?.Count ?? 0) <= AnswerFilterExpressions.MaxFilters)
            .WithMessage($"At most {AnswerFilterExpressions.MaxFilters} answer filters can be used.");
    }
}
