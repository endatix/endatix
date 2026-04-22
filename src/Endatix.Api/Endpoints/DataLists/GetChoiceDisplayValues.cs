using Endatix.Api.Common.FeatureFlags;
using Endatix.Api.Infrastructure;
using Endatix.Core.UseCases.DataLists.Search;
using Endatix.Framework.FeatureFlags;
using FastEndpoints;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Api.Endpoints.DataLists;

/// <summary>
/// Public endpoint to get choice display values.
/// </summary>
public sealed class GetChoiceDisplayValues(IMediator mediator)
    : Endpoint<GetChoiceDisplayValuesRequest, Results<Ok<IReadOnlyCollection<DataListPublicChoiceModel>>, ProblemHttpResult>>
{
    public override void Configure()
    {
        Get(ApiRoutes.Public("data-lists/{dataListId}/display-values"));
        AllowAnonymous();
        FeatureFlag<EndpointFeatureGate>(FeatureFlags.DataLists);
        Summary(s =>
        {
            s.Summary = "Resolve data list display values";
            s.Description = "Returns label/value pairs for provided values in a public data list.";
        });
    }

    public override async Task<Results<Ok<IReadOnlyCollection<DataListPublicChoiceModel>>, ProblemHttpResult>> ExecuteAsync(
        GetChoiceDisplayValuesRequest request,
        CancellationToken ct)
    {
        GetDataListChoiceDisplayValuesQuery query = new(request.DataListId, request.Values);
        var result = await mediator.Send(query, ct);

        return TypedResultsBuilder
            .MapResult(result, items => (IReadOnlyCollection<DataListPublicChoiceModel>)items.Select(DataListMapper.MapPublic).ToArray())
            .SetTypedResults<Ok<IReadOnlyCollection<DataListPublicChoiceModel>>, ProblemHttpResult>();
    }
}


/// <summary>
/// Request to get choice display values.
/// </summary>
public sealed class GetChoiceDisplayValuesRequest
{
    /// <summary>
    /// The ID of the data list.
    /// </summary>
    public long DataListId { get; init; }

    /// <summary>
    /// The values to get display values for.
    /// </summary>
    public IReadOnlyCollection<string> Values { get; init; } = [];
}

/// <summary>
/// Validator for the GetChoiceDisplayValuesRequest.
/// </summary>
public sealed class GetChoiceDisplayValuesValidator : Validator<GetChoiceDisplayValuesRequest>
{
    public GetChoiceDisplayValuesValidator()
    {
        RuleFor(x => x.DataListId).GreaterThan(0);
        RuleFor(x => x.Values)
            .NotNull()
            .Must(values => values.Count > 0)
            .WithMessage("'values' query parameter is required.");
    }
}
