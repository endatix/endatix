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
public sealed class GetDataListTranslationsCsvHandler(IRepository<DataList> repository)
    : IQueryHandler<GetDataListTranslationsCsvQuery, Result<DataListTranslationsCsvDto>>
{
    /// <inheritdoc />
    public async Task<Result<DataListTranslationsCsvDto>> Handle(
        GetDataListTranslationsCsvQuery request,
        CancellationToken cancellationToken)
    {
        DataListsSpecifications.ByIdWithItemsSpec spec = new(request.DataListId);
        var dataList = await repository.SingleOrDefaultAsync(spec, cancellationToken);
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
}
