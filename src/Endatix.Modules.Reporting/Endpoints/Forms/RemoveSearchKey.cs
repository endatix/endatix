using Endatix.Api.Infrastructure;
using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Modules.Reporting.Features.SearchCopy;
using Endatix.Modules.Reporting.Features.SubmissionGrid;
using FastEndpoints;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Modules.Reporting.Endpoints.Forms;

/// <summary>
/// Stops a column a person made filterable from being filtered in the submissions grid.
/// </summary>
public sealed class RemoveSearchKey(IMediator mediator, ITenantContext tenantContext)
    : Endpoint<RemoveSearchKeyRequest, Results<Ok<SubmissionGridSearchKeys>, ProblemHttpResult>>
{
    /// <inheritdoc />
    public override void Configure()
    {
        Delete("forms/{formId}/reporting/search-keys");
        Permissions(Actions.Forms.Edit);
        Summary(summary =>
        {
            summary.Summary = "Make a column no longer filterable";
            summary.Description =
                "Removes a column a person added from the keys answer filters read; filters on it are refused at " +
                "once. Columns chosen automatically cannot be removed. The key is a query parameter, because column " +
                "keys may hold characters a path segment cannot.";
            summary.Responses[200] = "The form's search keys after the change.";
            summary.Responses[400] = "Invalid input data, or a column chosen automatically.";
            summary.Responses[401] = "No tenant in the request.";
            summary.Responses[404] = "Form, reporting schema or search key not found.";
            summary.Responses[503] = "The form's reporting schema is being changed; try again.";
        });
        Description(builder => builder
            .Produces<SubmissionGridSearchKeys>(StatusCodes.Status200OK, "application/json")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable));
    }

    /// <inheritdoc />
    public override async Task<Results<Ok<SubmissionGridSearchKeys>, ProblemHttpResult>> ExecuteAsync(
        RemoveSearchKeyRequest request,
        CancellationToken ct)
    {
        RemoveSearchKeyCommand command = new(new SearchKeyTarget(tenantContext.TenantId, request.FormId, request.Key!));
        var result = await mediator.Send(command, ct);

        return TypedResultsBuilder
            .MapResult(result, searchKeys => searchKeys)
            .SetTypedResults<Ok<SubmissionGridSearchKeys>, ProblemHttpResult>();
    }
}

public sealed class RemoveSearchKeyValidator : Validator<RemoveSearchKeyRequest>
{
    public RemoveSearchKeyValidator()
    {
        RuleFor(request => request.FormId)
            .GreaterThan(0);

        RuleFor(request => request.Key)
            .NotEmpty()
            .MaximumLength(SubmissionGridColumnKeys.MaxKeyLength);
    }
}

public sealed class RemoveSearchKeyRequest
{
    public long FormId { get; init; }

    /// <summary>The column key, as the columns catalog lists it.</summary>
    [QueryParam]
    public string? Key { get; init; }
}
