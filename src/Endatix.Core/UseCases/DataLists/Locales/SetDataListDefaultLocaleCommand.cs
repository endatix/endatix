using Ardalis.GuardClauses;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.DataLists.Locales;

/// <summary>
/// Sets the real locale represented by the SurveyJS <c>default</c> label key.
/// </summary>
public sealed record SetDataListDefaultLocaleCommand : ICommand<Result<DataListDto>>
{
    public long DataListId { get; }
    public string DefaultLocale { get; }

    public SetDataListDefaultLocaleCommand(long dataListId, string defaultLocale)
    {
        Guard.Against.NegativeOrZero(dataListId);
        Guard.Against.NullOrWhiteSpace(defaultLocale);
        
        DataListId = dataListId;
        DefaultLocale = defaultLocale;
    }
}
