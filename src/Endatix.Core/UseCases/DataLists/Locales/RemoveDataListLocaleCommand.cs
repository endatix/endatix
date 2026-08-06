using Ardalis.GuardClauses;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.DataLists.Locales;

/// <summary>
/// Removes a locale from a data list catalog and strips that key from item labels.
/// </summary>
public sealed record RemoveDataListLocaleCommand : ICommand<Result<DataListDto>>
{
    public long DataListId { get; }
    public string Locale { get; }

    public RemoveDataListLocaleCommand(long dataListId, string locale)
    {
        Guard.Against.NegativeOrZero(dataListId);
        Guard.Against.NullOrWhiteSpace(locale);

        DataListId = dataListId;
        Locale = locale;
    }
}
