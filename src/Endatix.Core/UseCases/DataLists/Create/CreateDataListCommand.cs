using Ardalis.GuardClauses;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.DataLists.Create;

public sealed record CreateDataListCommand : ICommand<Result<DataList>>
{
    public string Name { get; init; }
    public string? Description { get; init; }

    public CreateDataListCommand(string name, string? description)
    {
        Guard.Against.NullOrWhiteSpace(name);
        Name = name;
        Description = description;
    }
}
