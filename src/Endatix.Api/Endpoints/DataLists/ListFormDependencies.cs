using Endatix.Api.Common.FeatureFlags;
using Endatix.Api.Endpoints.Forms;
using Endatix.Api.Infrastructure;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.UseCases.DataLists.ListFormDependencies;
using Endatix.Framework.FeatureFlags;
using FastEndpoints;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Api.Endpoints.DataLists;

public sealed class ListFormDependencies(
    IMediator mediator)
    : Endpoint<ListFormDependenciesRequest, Results<Ok<IEnumerable<FormModel>>, BadRequest, NotFound>>
{
    public override void Configure()
    {
        Get("data-lists/{dataListId}/forms");
        Permissions(Actions.Forms.View);
        FeatureFlag<EndpointFeatureGate>(FeatureFlags.DataLists);
    }

    public override async Task<Results<Ok<IEnumerable<FormModel>>, BadRequest, NotFound>> ExecuteAsync(
        ListFormDependenciesRequest request,
        CancellationToken ct)
    {
        ListFormDependenciesQuery query = new(request.DataListId);
        var result = await mediator.Send(query, ct);
        return TypedResultsBuilder
            .MapResult(result, forms => forms.ToFormModelList())
            .SetTypedResults<Ok<IEnumerable<FormModel>>, BadRequest, NotFound>();
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
    public ListFormDependenciesValidator()
    {
        RuleFor(x => x.DataListId).GreaterThan(0);
    }
}
