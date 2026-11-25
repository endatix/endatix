using Endatix.Core.Abstractions;
using Endatix.Core.Common.Translations;
using Endatix.Core.Entities;
using Endatix.Core.Events;
using Endatix.Core.Exceptions;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Endatix.Core.UseCases.DataLists.ReplaceItems;

/// <summary>
/// Handler for replacing items in a data list.
/// </summary>
[DirectCallOnly]
public sealed class ReplaceDataListItemsHandler : ICommandHandler<ReplaceDataListItemsCommand, Result<DataListDto>>
{
    private readonly DataListRows _lists;
    private readonly IMediator _mediator;
    private readonly ILogger<ReplaceDataListItemsHandler> _logger;

    /// <summary>Uses the supplied repositories for direct calls.</summary>
    public ReplaceDataListItemsHandler(IRepository<DataList> repository,
        IMediator mediator,
        ILogger<ReplaceDataListItemsHandler> logger)
        : this(new DataListRows(repository, ContentQueryScope.Repository), mediator, logger) { }

    private ReplaceDataListItemsHandler(DataListRows lists, IMediator mediator, ILogger<ReplaceDataListItemsHandler> logger)
    {
        _lists = lists;
        _mediator = mediator;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<DataListDto>> Handle(ReplaceDataListItemsCommand request, CancellationToken cancellationToken)
    {
        var dataList = await _lists.WithItemsAsync(request.DataListId, cancellationToken);
        if (dataList is null)
        {
            return Result.NotFound("Data list not found.");
        }

        var ensureErrors =
            DataListEnsureLocales.TryEnsure(dataList, request.EnsureLocales, _logger);
        if (ensureErrors is not null)
        {
            return Result.Invalid(ensureErrors);
        }

        if (!TryResolveItems(dataList, request.Items, out var resolvedItems, out var errors))
        {
            return Result.Invalid(errors);
        }

        var replaceFailure = TryReplaceItems(dataList, resolvedItems);
        if (replaceFailure is not null)
        {
            return replaceFailure;
        }

        try
        {
            await _lists.UpdateAsync(dataList, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Failed to persist data list items for data list {DataListId}", dataList.Id);
            return Result.Error("Failed to persist data list items.");
        }

        await _mediator.Publish(
            new DataListUpdatedEvent(dataList, DataListUpdateReasons.ItemsReplaced),
            cancellationToken);

        return Result.Success(DataListDtoMapper.FromEntity(dataList));
    }

    private bool TryResolveItems(
        DataList dataList,
        IReadOnlyCollection<ReplaceDataListItemInput> items,
        out List<(IReadOnlyDictionary<string, string> Labels, string Value)> resolvedItems,
        out List<ValidationError> errors)
    {
        errors = [];
        resolvedItems = [];

        foreach ((int i, var item) in items.Index())
        {
            var labels = item.ResolveLabels();
            CollectItemErrors(dataList, i, item, labels, errors);

            if (labels is not null && !string.IsNullOrWhiteSpace(item.Value))
            {
                resolvedItems.Add((labels, item.Value.Trim()));
            }
        }

        return errors.Count == 0;
    }

    private void CollectItemErrors(
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

    private void CollectLabelMapErrors(
        DataList dataList,
        int index,
        IReadOnlyDictionary<string, string> labels,
        List<ValidationError> errors)
    {
        foreach (var cultureKey in labels.Keys)
        {
            if (string.IsNullOrWhiteSpace(cultureKey))
            {
                continue;
            }

            // Same catalog gate as DataList.ValidateLabelKeys / AllowsTranslationKey.
            if (!CultureCode.TryParse(cultureKey, out var culture)
                || !dataList.AllowsTranslationKey(culture))
            {
                errors.Add(new()
                {
                    Identifier = $"Items[{index}].Labels.{cultureKey}",
                    ErrorMessage = $"Locale '{cultureKey}' is not in the data list AvailableLocales catalog."
                });
            }
        }

        try
        {
            _ = DataListItem.NormalizeLabels(labels);
        }
        catch (ArgumentException ex)
        {
            errors.Add(ToLabelValidationError(dataList, index, ex));
        }
    }

    /// <summary>
    /// Turns a label rejection from <see cref="DataListItem.NormalizeLabels"/> into a validation error.
    /// </summary>
    /// <remarks>
    /// The reason is read back off the exception through <see cref="SafeError.LogAndResolve"/> rather
    /// than re-derived here: <c>NormalizeLabels</c> throws <see cref="DomainValidationException"/>, so its
    /// author-written text is already the text the caller should see, and duplicating the conditions
    /// would only give the two copies a chance to disagree. Going through <c>LogAndResolve</c> keeps the
    /// diagnostic record for the other case - an <see cref="ArgumentException"/> that did not opt in is a
    /// defect, and the caller only ever sees the static fallback.
    /// </remarks>
    private ValidationError ToLabelValidationError(DataList dataList, int index, ArgumentException ex)
    {
        var labelsPrefix = $"Items[{index}].Labels";

        return new()
        {
            Identifier = ResolveLabelErrorIdentifier(labelsPrefix, ex),
            ErrorMessage = SafeError.LogAndResolve(
                _logger,
                ex,
                "Labels are not valid for this item.",
                $"normalizing labels for item {index} of data list {dataList.Id}")
        };
    }

    /// <summary>
    /// Points the error at the offending culture key when the throw named one; the whole-map throws
    /// name the <c>labels</c> parameter, which is attributed to the <c>default</c> entry they are about.
    /// </summary>
    private static string ResolveLabelErrorIdentifier(string labelsPrefix, ArgumentException ex)
    {
        if (IsConcreteLabelKey(ex.ParamName))
        {
            return $"{labelsPrefix}.{ex.ParamName}";
        }

        if (string.Equals(ex.ParamName, "labels", StringComparison.Ordinal))
        {
            return $"{labelsPrefix}.{SurveyJsTranslationKeys.DefaultKey}";
        }

        return labelsPrefix;
    }

    private static bool IsConcreteLabelKey(string? paramName) =>
        !string.IsNullOrEmpty(paramName)
        && !string.Equals(paramName, "labels", StringComparison.Ordinal)
        && !string.Equals(paramName, "cultureCode", StringComparison.Ordinal);

    private Result<DataListDto>? TryReplaceItems(
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
            _logger.LogWarning(ex, "Data list items were rejected for data list {DataListId}", dataList.Id);
            return Result.Invalid(new ValidationError
            {
                Identifier = "Items",
                ErrorMessage = "The data list items are invalid."
            });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Data list items were rejected for data list {DataListId}", dataList.Id);
            return Result.Invalid(new ValidationError
            {
                Identifier = "Items",
                ErrorMessage = "The data list items are invalid."
            });
        }
    }

    internal sealed class Registered(DataListRows lists, IMediator mediator, ILoggerFactory loggers)
        : RegisteredContentHandler<ReplaceDataListItemsCommand, Result<DataListDto>>(
            new ReplaceDataListItemsHandler(lists, mediator, loggers.CreateLogger<ReplaceDataListItemsHandler>()));
}
