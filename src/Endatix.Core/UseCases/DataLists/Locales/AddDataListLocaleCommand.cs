using Ardalis.GuardClauses;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.DataLists.Locales;

/// <summary>
/// Adds a culture to a data list catalog.
/// </summary>
public sealed record AddDataListLocaleCommand : ICommand<Result<DataListDto>>
{
    public long DataListId { get; }
    public string Locale { get; }

    public AddDataListLocaleCommand(long dataListId, string locale)
    {
        Guard.Against.NegativeOrZero(dataListId);
        Guard.Against.NullOrWhiteSpace(locale);
        
        DataListId = dataListId;
        Locale = locale;
    }
}
