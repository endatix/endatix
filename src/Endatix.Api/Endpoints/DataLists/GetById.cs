using Endatix.Api.Common.FeatureFlags;
using Endatix.Api.Infrastructure;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.UseCases.DataLists.GetById;
using Endatix.Framework.FeatureFlags;
using FastEndpoints;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Api.Endpoints.DataLists;

/// <summary>
/// Endpoint to get a data list by ID.
/// </summary>
public sealed class GetById(
    IMediator mediator
    )
    : Endpoint<GetDataListRequest, Results<Ok<DataListDetailsModel>, ProblemHttpResult>>
{
    /// <inheritdoc />
    public override void Configure()
    {
        Get("data-lists/{dataListId}");
        Permissions(Actions.Forms.View);
        FeatureFlag<EndpointFeatureGate>(FeatureFlags.DataLists);
    }

    /// <inheritdoc />
    public override async Task<Results<Ok<DataListDetailsModel>, ProblemHttpResult>> ExecuteAsync(GetDataListRequest request, CancellationToken ct)
    {
        GetDataListByIdQuery query = new(request.DataListId);
        var result = await mediator.Send(query, ct);

        return TypedResultsBuilder
            .MapResult(result, DataListMapper.MapDetails)
            .SetTypedResults<Ok<DataListDetailsModel>, ProblemHttpResult>();
    }
}


/// <summary>
/// Validator for the GetDataListRequest.
/// </summary>
public sealed class GetDataListValidator : Validator<GetDataListRequest>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GetDataListValidator"/> class.
    /// </summary>
    public GetDataListValidator()
    {
        RuleFor(x => x.DataListId).GreaterThan(0);
    }
}

/// <summary>
/// Request to get a data list by ID.
/// </summary>
public sealed class GetDataListRequest
{
    /// <summary>
    /// The ID of the data list to get.
    /// </summary>
    public long DataListId { get; init; }
}
