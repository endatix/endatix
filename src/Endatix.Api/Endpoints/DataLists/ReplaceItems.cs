using Endatix.Api.Infrastructure;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Entities;
using Endatix.Core.UseCases.DataLists.ReplaceItems;
using Endatix.Infrastructure.Data.Config;
using FastEndpoints;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Http;
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
        Summary(s =>
        {
            s.Summary = "Replace items in a data list";
            s.Description = "Replaces all items in a data list. Prefer Labels maps; Label is accepted as shorthand for { default: Label }.";
            s.ExampleRequest = new ReplaceDataListItemsRequest
            {
                DataListId = 1,
                Items =
                [
                    new ReplaceDataListItemRequest
                    {
                        Labels = new Dictionary<string, string>(StringComparer.Ordinal)
                        {
                            ["default"] = "New York",
                            ["es"] = "Nueva York"
                        },
                        Value = "NYC"
                    },
                    new ReplaceDataListItemRequest
                    {
                        Label = "Los Angeles",
                        Value = "LA"
                    }
                ]
            };
            s.ResponseExamples[200] = new DataListDetailsModel
            {
                Id = 1,
                Name = "Cities",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                ItemsCount = 2,
                DefaultLocale = "en",
                AvailableLocales = ["es"],
                Items =
                [
                    new DataListItemModel
                    {
                        Id = 10,
                        Labels = new Dictionary<string, string>(StringComparer.Ordinal)
                        {
                            ["default"] = "New York",
                            ["es"] = "Nueva York"
                        },
                        Label = "New York",
                        Value = "NYC"
                    },
                    new DataListItemModel
                    {
                        Id = 11,
                        Labels = new Dictionary<string, string>(StringComparer.Ordinal)
                        {
                            ["default"] = "Los Angeles"
                        },
                        Label = "Los Angeles",
                        Value = "LA"
                    }
                ]
            };
            s.Responses[200] = "Items replaced successfully.";
            s.Responses[400] = "Invalid input data.";
            s.Responses[404] = "Data list not found.";
        });
        Description(builder => builder
            .Produces<DataListDetailsModel>(StatusCodes.Status200OK, "application/json")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound));
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

    private static ReplaceDataListItemInput ToReplaceDataListItemInput(ReplaceDataListItemRequest request) => new(
        Value: request.Value ?? string.Empty,
        Labels: request.Labels,
        Label: request.Label);
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
        RuleFor(x => x.Items)
            .Must(items => items.Count <= 5_000)
            .WithMessage("A data list cannot have more than 5,000 items.")
            .When(x => x.Items != null);

        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(x => x.Value)
                .NotEmpty()
                .MaximumLength(DataSchemaConstants.MAX_NAME_LENGTH);

            item.RuleFor(x => x)
                .Must(HasLabelsOrLabel)
                .WithMessage("Either Labels or Label is required.");

            item.RuleFor(x => x.Label)
                .MaximumLength(DataSchemaConstants.MAX_NAME_LENGTH)
                .When(x => !string.IsNullOrWhiteSpace(x.Label));

            item.RuleFor(x => x.Labels)
                .Must(labels => labels is null || labels.Values.All(v =>
                    string.IsNullOrWhiteSpace(v) || v.Trim().Length <= DataListItem.MAX_LABEL_LENGTH))
                .WithMessage($"Each label value cannot exceed {DataListItem.MAX_LABEL_LENGTH} characters.");
        });
    }

    private static bool HasLabelsOrLabel(ReplaceDataListItemRequest item) =>
        (item.Labels is not null && item.Labels.Count > 0)
        || !string.IsNullOrWhiteSpace(item.Label);
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
    /// Localized labels including <c>default</c>. Preferred over <see cref="Label"/>.
    /// </summary>
    public Dictionary<string, string>? Labels { get; init; }

    /// <summary>
    /// Legacy monolingual label (maps to Labels.default).
    /// </summary>
    public string? Label { get; init; }

    /// <summary>
    /// The value of the item to replace.
    /// </summary>
    public string? Value { get; init; }
}
