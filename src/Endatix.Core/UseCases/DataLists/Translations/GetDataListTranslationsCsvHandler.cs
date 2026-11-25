using Endatix.Core.Abstractions;
using Endatix.Core.Common;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;

namespace Endatix.Core.UseCases.DataLists.Translations;

/// <summary>
/// Handler exporting a data list to a SurveyJS-compatible translations CSV.
/// </summary>
[DirectCallOnly]
public sealed class GetDataListTranslationsCsvHandler : IQueryHandler<GetDataListTranslationsCsvQuery, Result<DataListTranslationsCsvDto>>
{
    private readonly DataListRows _lists;

    /// <summary>Uses the supplied repositories for direct calls.</summary>
    public GetDataListTranslationsCsvHandler(IRepository<DataList> repository)
        : this(new DataListRows(repository, ContentQueryScope.Repository)) { }

    private GetDataListTranslationsCsvHandler(DataListRows lists)
    {
        _lists = lists;
    }

    /// <inheritdoc />
    public async Task<Result<DataListTranslationsCsvDto>> Handle(
        GetDataListTranslationsCsvQuery request,
        CancellationToken cancellationToken)
    {
        var dataList = await _lists.WithItemsAsync(request.DataListId, cancellationToken);
        if (dataList is null)
        {
            return Result.NotFound("Data list not found.");
        }

        var columns = DataListTranslationsCsv.BuildColumns(dataList);
        var rows = dataList.Items
            .OrderBy(item => item.Value, StringComparer.Ordinal)
            .Select(item => new DataListTranslationRow(item.Value, item.Labels));

        var csv = DataListTranslationsCsv.Serialize(columns, rows);

        return Result.Success(new DataListTranslationsCsvDto(csv, BuildFileName(dataList)));
    }

    private static string BuildFileName(DataList dataList)
    {
        var slug = UrlSlugNormalizer.Normalize(dataList.NormalizedName);

        return string.IsNullOrEmpty(slug)
            ? $"data-list-{dataList.Id}-translations.csv"
            : $"{slug}-translations.csv";
    }

    internal sealed class Registered(DataListRows lists)
        : RegisteredContentHandler<GetDataListTranslationsCsvQuery, Result<DataListTranslationsCsvDto>>(
            new GetDataListTranslationsCsvHandler(lists));
}
