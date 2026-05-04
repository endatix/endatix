using Endatix.Api.Endpoints.Forms;
using Endatix.Api.Infrastructure;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.UseCases.DataLists.ListFormDependencies;
using FastEndpoints;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Api.Endpoints.DataLists;

/// <summary>
/// Endpoint to list form dependencies for a data list.
/// </summary>
public sealed class ListFormDependencies(
    IMediator mediator)
    : Endpoint<ListFormDependenciesRequest, Results<Ok<IEnumerable<FormModel>>, ProblemHttpResult>>
{
    /// <inheritdoc />
    public override void Configure()
    {
        Get("data-lists/{dataListId}/forms");
        Permissions(Actions.Forms.View);
        Summary(s =>
        {
            s.Summary = "List form dependencies for a data list";
            s.Description = "Lists the form dependencies for a data list.";
            s.Responses[200] = "Form dependencies listed successfully.";
            s.Responses[400] = "Invalid request or access data.";
            s.Responses[404] = "Data list not found.";
        });
    }

    /// <inheritdoc />
    public override async Task<Results<Ok<IEnumerable<FormModel>>, ProblemHttpResult>> ExecuteAsync(
        ListFormDependenciesRequest request,
        CancellationToken ct)
    {
        ListFormDependenciesQuery query = new(request.DataListId);
        var result = await mediator.Send(query, ct);
        
        return TypedResultsBuilder
            .MapResult(result, forms => forms.ToFormModelList())
            .SetTypedResults<Ok<IEnumerable<FormModel>>, ProblemHttpResult>();
    }
}

/// <summary>
/// Request to list form dependencies.
/// </summary>
public sealed class ListFormDependenciesRequest
{
    /// <summary>
    /// The ID of the data list to list form dependencies for.
    /// </summary>
    public long DataListId { get; init; }
}

/// <summary>
/// Validator for the ListFormDependenciesRequest.
/// </summary>
public sealed class ListFormDependenciesValidator : Validator<ListFormDependenciesRequest>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ListFormDependenciesValidator"/> class.
    /// </summary>
    public ListFormDependenciesValidator()
    {
        RuleFor(x => x.DataListId).GreaterThan(0);
    }
}
