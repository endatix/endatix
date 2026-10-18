using Endatix.Api.Infrastructure;
using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Common.Translations;
using Endatix.Modules.Reporting.Domain.SurveyJs;
using Endatix.Modules.Reporting.Features.SubmissionGrid;
using FastEndpoints;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Modules.Reporting.Endpoints.Forms;

/// <summary>
/// Lists the submissions-grid columns of a form's compiled reporting schema.
/// </summary>
public sealed class ListGridColumns(
    ListSubmissionGridColumns listSubmissionGridColumns,
    ITenantContext tenantContext)
    : Endpoint<ListGridColumnsRequest, Results<Ok<SubmissionGridColumns>, ProblemHttpResult>>
{
    /// <inheritdoc />
    public override void Configure()
    {
        Get("forms/{formId}/reporting/columns");
        Permissions(Actions.Submissions.View);
        Summary(summary =>
        {
            summary.Summary = "List submissions grid columns";
            summary.Description =
                "Returns the columns of the form's compiled reporting schema in schema order: key, title in the " +
                "requested locale, question type, answer filter type, and page and panel. Also returns how many " +
                "submissions are prepared. A form without a compiled schema returns schemaReady false and no columns.";
            summary.Responses[200] = "Columns returned.";
            summary.Responses[400] = "Invalid input data.";
            summary.Responses[401] = "No tenant in the request.";
            summary.Responses[404] = "Form not found.";
        });
        Description(builder => builder
            .Produces<SubmissionGridColumns>(StatusCodes.Status200OK, "application/json")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound));
    }

    /// <inheritdoc />
    public override async Task<Results<Ok<SubmissionGridColumns>, ProblemHttpResult>> ExecuteAsync(
        ListGridColumnsRequest request,
        CancellationToken ct)
    {
        SubmissionGridColumnsQuery query = new(
            tenantContext.TenantId,
            request.FormId,
            request.Locale ?? SurveyJsElementType.DefaultLocale);
        var result = await listSubmissionGridColumns.ExecuteAsync(query, ct);

        return TypedResultsBuilder
            .MapResult(result, columns => columns)
            .SetTypedResults<Ok<SubmissionGridColumns>, ProblemHttpResult>();
    }
}

/// <summary>
/// Validates the request for the list grid columns endpoint.
/// </summary>
public sealed class ListGridColumnsValidator : Validator<ListGridColumnsRequest>
{
    public ListGridColumnsValidator()
    {
        RuleFor(request => request.FormId)
            .GreaterThan(0);

        RuleFor(request => request.Locale)
            .MaximumLength(IHasTranslations.MAX_CULTURE_CODE_LENGTH);
    }
}

/// <summary>
/// The request for the list grid columns endpoint.
/// </summary>
public sealed class ListGridColumnsRequest
{
    /// <summary>The form whose columns to list.</summary>
    public long FormId { get; init; }

    /// <summary>The locale of the titles; the default locale when omitted or unknown.</summary>
    [QueryParam]
    public string? Locale { get; init; }
}
