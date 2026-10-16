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
/// Makes a column of a form filterable in the submissions grid, in one of the slots kept for people.
/// </summary>
public sealed class AddSearchKey(IMediator mediator, ITenantContext tenantContext)
    : Endpoint<AddSearchKeyRequest, Results<Ok<SubmissionGridSearchKeys>, ProblemHttpResult>>
{
    /// <inheritdoc />
    public override void Configure()
    {
        Post("forms/{formId}/reporting/search-keys");
        Permissions(Actions.Forms.Edit);
        Summary(summary =>
        {
            summary.Summary = "Make a column filterable";
            summary.Description =
                "Adds a column of the form's reporting schema to the keys answer filters read, beside those chosen " +
                $"automatically. People may add {FormSearchKeys.MaxUserKeys}, and a form has at most " +
                $"{FormSearchKeys.MaxKeys}. Read-only questions may be added. The form's prepared rows are rebuilt " +
                "for the new key before the response; rows not rebuilt yet are reported as pending and count as not " +
                "checked. Adding a key the form already has changes nothing and finishes a rebuild that was cut off.";
            summary.Responses[200] = "The form's search keys after the change.";
            summary.Responses[400] = "Invalid input data, a column no answer filter supports, or no free slot.";
            summary.Responses[401] = "No tenant in the request.";
            summary.Responses[404] = "Form or reporting schema not found.";
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
        AddSearchKeyRequest request,
        CancellationToken ct)
    {
        AddSearchKeyCommand command = new(new SearchKeyTarget(tenantContext.TenantId, request.FormId, request.Key!));
        var result = await mediator.Send(command, ct);

        return TypedResultsBuilder
            .MapResult(result, searchKeys => searchKeys)
            .SetTypedResults<Ok<SubmissionGridSearchKeys>, ProblemHttpResult>();
    }
}

public sealed class AddSearchKeyValidator : Validator<AddSearchKeyRequest>
{
    public AddSearchKeyValidator()
    {
        RuleFor(request => request.FormId)
            .GreaterThan(0);

        RuleFor(request => request.Key)
            .NotEmpty()
            .MaximumLength(SubmissionGridColumnKeys.MaxKeyLength);
    }
}

public sealed class AddSearchKeyRequest
{
    public long FormId { get; init; }

    /// <summary>The column key, as the columns catalog lists it.</summary>
    public string? Key { get; init; }
}
