using Ardalis.GuardClauses;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.DataLists.UpdateDetails;

/// <summary>
/// Command to partially update a data list name and/or description.
/// </summary>
public sealed record UpdateDataListDetailsCommand : ICommand<Result<DataListDto>>
{
    public long DataListId { get; init; }

    /// <summary>
    /// When set, replaces the data list name.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// When set, replaces the data list description (use empty string to clear).
    /// </summary>
    public string? Description { get; init; }

    public UpdateDataListDetailsCommand(long dataListId, string? name, string? description)
    {
        Guard.Against.NegativeOrZero(dataListId);
        DataListId = dataListId;
        Name = name;
        Description = description;
    }
}
