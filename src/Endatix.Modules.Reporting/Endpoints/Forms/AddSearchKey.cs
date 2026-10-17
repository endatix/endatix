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
                $"{FormSearchKeys.MaxKeys}. Read-only questions may be added. The response comes at once, with the " +
                "key pending; the form's prepared rows are rebuilt for it in the background, and until then they " +
                "count as not checked. Adding a key the form already has changes nothing and asks for the rebuild " +
                "again, which finishes one that was cut off.";
            summary.Responses[400] = "Invalid input data, a column no answer filter supports, or no free slot.";
            summary.Responses[404] = "Form or reporting schema not found.";
            SearchKeyEndpointDocs.DescribeSharedResponses(summary);
        });
        Description(builder => builder.ProducesSearchKeys());
    }

    /// <inheritdoc />
    public override async Task<Results<Ok<SubmissionGridSearchKeys>, ProblemHttpResult>> ExecuteAsync(
        AddSearchKeyRequest request,
        CancellationToken ct)
    {
        AddSearchKeyCommand command = new(new SearchKeyTarget(tenantContext.TenantId, request.FormId, request.Key));
        var result = await mediator.Send(command, ct);

        return TypedResultsBuilder
            .MapResult(result, searchKeys => searchKeys)
            .SetTypedResults<Ok<SubmissionGridSearchKeys>, ProblemHttpResult>();
    }
}

/// <summary>
/// Validates the request for the add search key endpoint.
/// </summary>
public sealed class AddSearchKeyValidator : Validator<AddSearchKeyRequest>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AddSearchKeyValidator"/> class.
    /// </summary>
    public AddSearchKeyValidator()
    {
        RuleFor(request => request.FormId)
            .GreaterThan(0);

        RuleFor(request => request.Key)
            .NotEmpty()
            .MaximumLength(SubmissionGridColumnKeys.MaxKeyLength);
    }
}

/// <summary>
/// The request for the add search key endpoint.
/// </summary>
public sealed class AddSearchKeyRequest
{
    /// <summary>The form whose search keys to change.</summary>
    public long FormId { get; init; }

    /// <summary>The column key, as the columns catalog lists it.</summary>
    public string Key { get; init; } = string.Empty;
}
