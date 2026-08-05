using Endatix.Core.Entities;
using Endatix.Core.Events;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using MediatR;

namespace Endatix.Core.UseCases.DataLists.ReplaceItems;

/// <summary>
/// Handler for replacing items in a data list.
/// </summary>
public sealed class ReplaceDataListItemsHandler(
    IRepository<DataList> repository,
    IMediator mediator
    )
    : ICommandHandler<ReplaceDataListItemsCommand, Result<DataListDto>>
{
    /// <inheritdoc />
    public async Task<Result<DataListDto>> Handle(ReplaceDataListItemsCommand request, CancellationToken cancellationToken)
    {
        DataListsSpecifications.ByIdWithItemsSpec spec = new(request.DataListId);
        DataList? dataList = await repository.SingleOrDefaultAsync(spec, cancellationToken);
        if (dataList is null)
        {
            return Result.NotFound("Data list not found.");
        }

        if (!TryResolveItems(dataList, request.Items, out List<(IReadOnlyDictionary<string, string> Labels, string Value)> resolvedItems, out List<ValidationError> errors))
        {
            return Result.Invalid(errors);
        }

        Result<DataListDto>? replaceFailure = TryReplaceItems(dataList, resolvedItems);
        if (replaceFailure is not null)
        {
            return replaceFailure;
        }

        await repository.UpdateAsync(dataList, cancellationToken);
        await mediator.Publish(
            new DataListUpdatedEvent(dataList, DataListUpdateReasons.ItemsReplaced),
            cancellationToken);

        return Result.Success(DataListDtoMapper.FromEntity(dataList));
    }

    private static bool TryResolveItems(
        DataList dataList,
        IReadOnlyCollection<ReplaceDataListItemInput> items,
        out List<(IReadOnlyDictionary<string, string> Labels, string Value)> resolvedItems,
        out List<ValidationError> errors)
    {
        errors = [];
        resolvedItems = [];

        foreach ((int i, ReplaceDataListItemInput item) in items.Index())
        {
            IReadOnlyDictionary<string, string>? labels = item.ResolveLabels();
            CollectItemErrors(dataList, i, item, labels, errors);

            if (labels is not null && !string.IsNullOrWhiteSpace(item.Value))
            {
                resolvedItems.Add((labels, item.Value.Trim()));
            }
        }

        return errors.Count == 0;
    }

    private static void CollectItemErrors(
        DataList dataList,
        int index,
        ReplaceDataListItemInput item,
        IReadOnlyDictionary<string, string>? labels,
        List<ValidationError> errors)
    {
        if (labels is null)
        {
            errors.Add(new()
            {
                Identifier = $"Items[{index}].Labels",
                ErrorMessage = "Labels (or legacy Label) is required."
            });
        }
        else if (!labels.TryGetValue(DataListItem.DefaultLabelKey, out var defaultLabel)
                 || string.IsNullOrWhiteSpace(defaultLabel))
        {
            errors.Add(new()
            {
                Identifier = $"Items[{index}].Labels.default",
                ErrorMessage = "Labels must include a non-empty 'default' entry."
            });
        }
        else
        {
            CollectLabelMapErrors(dataList, index, labels, errors);
        }

        if (string.IsNullOrWhiteSpace(item.Value))
        {
            errors.Add(new()
            {
                Identifier = $"Items[{index}].Value",
                ErrorMessage = "Value is required."
            });
        }
    }

    private static void CollectLabelMapErrors(
        DataList dataList,
        int index,
        IReadOnlyDictionary<string, string> labels,
        List<ValidationError> errors)
    {
        foreach ((string cultureKey, string labelValue) in labels)
        {
            if (string.IsNullOrWhiteSpace(cultureKey))
            {
                continue;
            }

            if (!dataList.AllowsTranslationKey(cultureKey))
            {
                errors.Add(new()
                {
                    Identifier = $"Items[{index}].Labels.{cultureKey}",
                    ErrorMessage = $"Culture '{cultureKey}' is not in the data list culture catalog."
                });
            }

            if (!string.IsNullOrWhiteSpace(labelValue)
                && labelValue.Trim().Length > DataListItem.MAX_LABEL_LENGTH)
            {
                errors.Add(new()
                {
                    Identifier = $"Items[{index}].Labels.{cultureKey}",
                    ErrorMessage = $"Each label value cannot exceed {DataListItem.MAX_LABEL_LENGTH} characters."
                });
            }
        }
    }

    private static Result<DataListDto>? TryReplaceItems(
        DataList dataList,
        IReadOnlyList<(IReadOnlyDictionary<string, string> Labels, string Value)> resolvedItems)
    {
        try
        {
            dataList.ReplaceItems(resolvedItems);
            return null;
        }
        catch (ArgumentException ex)
        {
            return Result.Invalid(new ValidationError
            {
                Identifier = "Items",
                ErrorMessage = ex.Message
            });
        }
    }
}
