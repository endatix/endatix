using Ardalis.GuardClauses;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.DataLists.Translations;

/// <summary>
/// Query to export data list translations as CSV.
/// </summary>
public sealed record GetDataListTranslationsCsvQuery : IQuery<Result<DataListTranslationsCsvDto>>
{
    /// <summary>
    /// The ID of the data list to export.
    /// </summary>
    public long DataListId { get; init; }

    public GetDataListTranslationsCsvQuery(long dataListId)
    {
        Guard.Against.NegativeOrZero(dataListId);

        DataListId = dataListId;
    }
}

/// <summary>
/// Exported translations CSV together with a suggested download file name.
/// </summary>
public sealed record DataListTranslationsCsvDto(string Csv, string FileName);
