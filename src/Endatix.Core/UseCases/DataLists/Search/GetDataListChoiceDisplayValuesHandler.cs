using Endatix.Core.Common.Translations;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;

namespace Endatix.Core.UseCases.DataLists.Search;

/// <summary>
/// Handler to get data list choice display values.
/// </summary>
public sealed class GetDataListChoiceDisplayValuesHandler(IRepository<DataList> repository)
    : IQueryHandler<GetDataListChoiceDisplayValuesQuery, Result<IReadOnlyCollection<DataListChoiceDisplayValueDto>>>
{
    public async Task<Result<IReadOnlyCollection<DataListChoiceDisplayValueDto>>> Handle(
        GetDataListChoiceDisplayValuesQuery request,
        CancellationToken cancellationToken)
    {
        string[] requestedValues = [.. request.Values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)];

        DataListsSpecifications.ByIdWithItemsByValuesSpec spec = new(request.DataListId, requestedValues);
        var dataList = await repository.SingleOrDefaultAsync(spec, cancellationToken);
        if (dataList is null)
        {
            return Result.NotFound("Data list not found.");
        }

        var labelKeys = BuildLabelKeys(dataList, request.IncludeLocales);

        IReadOnlyCollection<DataListChoiceDisplayValueDto> items = [.. dataList.Items
            .OrderBy(item => item.DefaultLabel)
            .ThenBy(item => item.Value)
            .Select(item => new DataListChoiceDisplayValueDto(
                item.Value,
                ProjectLabels(item, labelKeys)))];

        return Result.Success(items);
    }

    private static IReadOnlyList<string> BuildLabelKeys(DataList dataList, IReadOnlyList<CultureCode> includeLocales)
    {
        List<string> keys = [SurveyJsTranslationKeys.DefaultKey];
        foreach (var key in dataList.ResolveTranslationKeys(includeLocales))
        {
            if (!keys.Contains(key, StringComparer.Ordinal))
            {
                keys.Add(key);
            }
        }

        return keys;
    }

    private static IReadOnlyDictionary<string, string> ProjectLabels(DataListItem item, IReadOnlyList<string> labelKeys)
    {
        Dictionary<string, string> labels = new(labelKeys.Count, StringComparer.Ordinal);
        foreach (var key in labelKeys)
        {
            if (item.Labels.TryGetValue(key, out var label))
            {
                labels[key] = label;
            }
        }

        labels.TryAdd(SurveyJsTranslationKeys.DefaultKey, item.DefaultLabel);

        return labels;
    }
}
