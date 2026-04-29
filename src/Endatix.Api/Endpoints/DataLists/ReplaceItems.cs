using Endatix.Api.Common.FeatureFlags;
using Endatix.Api.Infrastructure;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.UseCases.DataLists.ReplaceItems;
using Endatix.Framework.FeatureFlags;
using Endatix.Infrastructure.Data.Config;
using FastEndpoints;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Api.Endpoints.DataLists;

/// <summary>
/// Endpoint to replace items in a data list.
/// </summary>
public sealed class ReplaceItems(
    IMediator mediator)
    : Endpoint<ReplaceDataListItemsRequest, Results<Ok<DataListDetailsModel>, ProblemHttpResult>>
{
    /// <summary>
    /// Configures the endpoint settings.
    /// </summary>
    public override void Configure()
    {
        Put("data-lists/{dataListId}/items");
        Permissions(Actions.Forms.Edit);
        FeatureFlag<EndpointFeatureGate>(FeatureFlags.DataLists);
    }

    /// <inheritdoc />
    public override async Task<Results<Ok<DataListDetailsModel>, ProblemHttpResult>> ExecuteAsync(ReplaceDataListItemsRequest request, CancellationToken ct)
    {
        var command = new ReplaceDataListItemsCommand(
            request.DataListId,
            [.. request.Items.Select(ToReplaceDataListItemInput)]);

        var result = await mediator.Send(command, ct);

        return TypedResultsBuilder
            .MapResult(result, DataListMapper.MapDetails)
            .SetTypedResults<Ok<DataListDetailsModel>, ProblemHttpResult>();
    }

    private ReplaceDataListItemInput ToReplaceDataListItemInput(ReplaceDataListItemRequest request) => new(
        request.Label ?? string.Empty,
        request.Value ?? string.Empty);

}


/// <summary>
/// Validator for the ReplaceDataListItemsRequest.
/// </summary>
public sealed class ReplaceDataListItemsValidator : Validator<ReplaceDataListItemsRequest>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ReplaceDataListItemsValidator"/> class.
    /// </summary>
    public ReplaceDataListItemsValidator()
    {
        RuleFor(x => x.DataListId).GreaterThan(0);
        RuleFor(x => x.Items).NotNull();

        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(x => x.Label)
                .NotEmpty()
                .MaximumLength(DataSchemaConstants.MAX_NAME_LENGTH);

            item.RuleFor(x => x.Value)
                .NotEmpty()
                .MaximumLength(DataSchemaConstants.MAX_NAME_LENGTH);
        });
    }
}


/// <summary>
/// Request to replace items in a data list.
/// </summary>
public sealed class ReplaceDataListItemsRequest
{
    /// <summary>
    /// The ID of the data list to replace items for.
    /// </summary>
    public long DataListId { get; init; }

    /// <summary>
    /// The items to replace in the data list.
    /// </summary>
    public IReadOnlyCollection<ReplaceDataListItemRequest> Items { get; init; } = [];
}

/// <summary>
/// Request to replace a single item in a data list.
/// </summary>
public sealed class ReplaceDataListItemRequest
{
    /// <summary>
    /// The label of the item to replace.
    /// </summary>
    public string? Label { get; init; }

    /// <summary>
    /// The value of the item to replace.
    /// </summary>
    public string? Value { get; init; }
}