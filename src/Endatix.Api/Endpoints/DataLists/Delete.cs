using Endatix.Api.Common.FeatureFlags;
using Endatix.Api.Infrastructure;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.UseCases.DataLists.Delete;
using Endatix.Framework.FeatureFlags;
using FastEndpoints;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Api.Endpoints.DataLists;

/// <summary>
/// Endpoint to delete a data list.
/// </summary>
public sealed class Delete(
    IMediator mediator)
    : Endpoint<DeleteDataListRequest, Results<Ok<string>, BadRequest, NotFound>>
{
    public override void Configure()
    {
        Delete("data-lists/{dataListId}");
        Permissions(Actions.Forms.Delete);
        FeatureFlag<EndpointFeatureGate>(FeatureFlags.DataLists);
    }

    public override async Task<Results<Ok<string>, BadRequest, NotFound>> ExecuteAsync(DeleteDataListRequest request, CancellationToken ct)
    {
        DeleteDataListCommand deleteCommand = new(request.DataListId);
        var result = await mediator.Send(deleteCommand, ct);

        return TypedResultsBuilder
            .MapResult(result, dataList => dataList.Id.ToString())
            .SetTypedResults<Ok<string>, BadRequest, NotFound>();
    }
}


/// <summary>
/// Validator for the DeleteDataListRequest.
/// </summary>
public sealed class DeleteDataListValidator : Validator<DeleteDataListRequest>
{
    public DeleteDataListValidator()
    {
        RuleFor(x => x.DataListId).GreaterThan(0);
    }
}

/// <summary>
/// Request to delete a data list.
/// </summary>
public sealed class DeleteDataListRequest
{
    /// <summary>
    /// The ID of the data list to delete.
    /// </summary>
    public long DataListId { get; init; }
}

