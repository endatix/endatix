using Ardalis.GuardClauses;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.Folders.Create;

/// <summary> 
/// Command for creating a folder
/// </summary>
public sealed record CreateFolderCommand : ICommand<Result<Folder>>
{
    public string Name { get; }
    public string? Slug { get; }
    public string? Description { get; }
    public string? Metadata { get; }
    public bool Immutable { get; }

    public CreateFolderCommand(string name, string? slug, string? description, string? metadata = null, bool immutable = false)
    {
        Guard.Against.NullOrWhiteSpace(name);
        Name = name;
        Slug = slug;
        Description = description;
        Metadata = metadata;
        Immutable = immutable;
    }
}
