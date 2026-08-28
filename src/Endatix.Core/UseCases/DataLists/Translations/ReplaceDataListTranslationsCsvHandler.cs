using Endatix.Core.Abstractions;
using Endatix.Core.Common.Translations;
using Endatix.Core.Entities;
using Endatix.Core.Events;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Endatix.Core.UseCases.DataLists.Translations;

/// <summary>
/// Handler replacing data list items and translations from a CSV document.
/// </summary>
public sealed class ReplaceDataListTranslationsCsvHandler(
    IRepository<DataList> repository,
    IMediator mediator,
    IIdGenerator<long> idGenerator,
    ILogger<ReplaceDataListTranslationsCsvHandler> logger)
    : ICommandHandler<ReplaceDataListTranslationsCsvCommand, Result<DataListDto>>
{
    /// <inheritdoc />
    public async Task<Result<DataListDto>> Handle(
        ReplaceDataListTranslationsCsvCommand request,
        CancellationToken cancellationToken)
    {
        DataListsSpecifications.ByIdWithItemsSpec spec = new(request.DataListId);
        var dataList = await repository.SingleOrDefaultAsync(spec, cancellationToken);
        if (dataList is null)
        {
            return Result.NotFound("Data list not found.");
        }

        var ensureErrors =
            DataListEnsureLocales.TryEnsure(dataList, request.EnsureLocales);
        if (ensureErrors is not null)
        {
            return Result.Invalid(ensureErrors);
        }

        DataListTranslationsCsvDocument document;
        try
        {
            document = DataListTranslationsCsv.Parse(request.Csv);
        }
        catch (FormatException ex)
        {
            logger.LogWarning(ex, "Failed to parse data list translations CSV for data list {DataListId}", request.DataListId);
            return Invalid("Csv", "The translations CSV is invalid.");
        }

        if (document.Rows.Count > ReplaceDataListTranslationsCsvCommand.MAX_ROWS)
        {
            return Invalid("Csv", $"A translations CSV cannot have more than {ReplaceDataListTranslationsCsvCommand.MAX_ROWS:N0} rows.");
        }

        List<ValidationError> errors = [];
        var labelKeys = ResolveColumns(dataList, document.Columns, errors);
        var items =
            BuildItems(document, labelKeys, errors);

        if (errors.Count > 0)
        {
            return Result.Invalid(errors);
        }

        try
        {
            dataList.ReplaceItems(items, idGenerator.CreateId);
        }
        catch (ArgumentException ex)
        {
            logger.LogWarning(ex, "Data list translations CSV was rejected for data list {DataListId}", request.DataListId);
            return Invalid("Csv", "The translations CSV is invalid.");
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(ex, "Data list translations CSV was rejected for data list {DataListId}", request.DataListId);
            return Invalid("Csv", "The translations CSV is invalid.");
        }

        try
        {
            await repository.UpdateAsync(dataList, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            logger.LogError(ex, "Failed to persist data list translations for data list {DataListId}", request.DataListId);
            return Result.Error("Failed to persist data list items.");
        }

        await mediator.Publish(
            new DataListUpdatedEvent(dataList, DataListUpdateReasons.ItemsReplaced),
            cancellationToken);

        return Result.Success(DataListDtoMapper.FromEntity(dataList));
    }

    /// <summary>
    /// Maps CSV column names to label keys, rejecting anything outside the culture catalog.
    /// Returns an empty array when the columns are unusable.
    /// </summary>
    private static string[] ResolveColumns(
        DataList dataList,
        IReadOnlyList<string> columns,
        List<ValidationError> errors)
    {
        var keys = new string[columns.Count];
        HashSet<string> seen = new(StringComparer.Ordinal);

        for (var i = 0; i < columns.Count; i++)
        {
            var column = columns[i];
            var key = ResolveColumnKey(dataList, column);
            if (key is null)
            {
                errors.Add(new ValidationError
                {
                    Identifier = $"Columns.{column}",
                    ErrorMessage = $"Column '{column}' is not in the data list AvailableLocales catalog."
                });
                continue;
            }

            if (!seen.Add(key))
            {
                errors.Add(new ValidationError
                {
                    Identifier = $"Columns.{column}",
                    ErrorMessage = $"Column '{column}' duplicates label key '{key}'."
                });
                continue;
            }

            keys[i] = key;
        }

        if (!seen.Contains(SurveyJsTranslationKeys.DefaultKey))
        {
            errors.Add(new ValidationError
            {
                Identifier = "Columns.default",
                ErrorMessage = $"A '{SurveyJsTranslationKeys.DefaultKey}' column is required."
            });
        }

        return errors.Count == 0 ? keys : [];
    }

    private static string? ResolveColumnKey(DataList dataList, string column)
    {
        if (!CultureCode.TryParse(column, out var culture))
        {
            return null;
        }

        return dataList.TryResolveLabelKey(culture, out var labelKey) ? labelKey : null;
    }

    private static List<(IReadOnlyDictionary<string, string> Labels, string Value)> BuildItems(
        DataListTranslationsCsvDocument document,
        string[] labelKeys,
        List<ValidationError> errors)
    {
        List<(IReadOnlyDictionary<string, string> Labels, string Value)> items = [];
        if (labelKeys.Length == 0)
        {
            return items;
        }

        HashSet<string> values = new(StringComparer.OrdinalIgnoreCase);

        foreach ((var index, var row) in document.Rows.Index())
        {
            if (string.IsNullOrWhiteSpace(row.Value))
            {
                errors.Add(RowError(index, DataListTranslationsCsv.ValueColumn, "Value is required."));
                continue;
            }

            if (!values.Add(row.Value))
            {
                errors.Add(RowError(index, DataListTranslationsCsv.ValueColumn, $"Value '{row.Value}' is duplicated."));
                continue;
            }

            Dictionary<string, string> labels = new(StringComparer.Ordinal);
            for (var column = 0; column < labelKeys.Length; column++)
            {
                if (!row.Labels.TryGetValue(document.Columns[column], out var label))
                {
                    continue;
                }

                // Quoted whitespace-only cells survive Parse; treat them as empty like unquoted blanks.
                var trimmed = label.Trim();
                if (trimmed.Length == 0)
                {
                    continue;
                }

                if (trimmed.Length > DataListItem.MAX_LABEL_LENGTH)
                {
                    errors.Add(RowError(
                        index,
                        labelKeys[column],
                        $"Each label value cannot exceed {DataListItem.MAX_LABEL_LENGTH} characters."));
                    continue;
                }

                labels[labelKeys[column]] = trimmed;
            }

            if (!labels.ContainsKey(SurveyJsTranslationKeys.DefaultKey))
            {
                errors.Add(RowError(index, SurveyJsTranslationKeys.DefaultKey, "A default label is required."));
                continue;
            }

            items.Add((labels, row.Value));
        }

        return items;
    }

    private static ValidationError RowError(int index, string column, string message) => new()
    {
        Identifier = $"Rows[{index}].{column}",
        ErrorMessage = message
    };

    private static Result<DataListDto> Invalid(string identifier, string message) =>
        Result.Invalid(new ValidationError
        {
            Identifier = identifier,
            ErrorMessage = message
        });
}
