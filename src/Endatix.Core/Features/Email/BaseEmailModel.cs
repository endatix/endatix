using System.Collections.Generic;
using Ardalis.GuardClauses;
using Endatix.Core.Abstractions;

namespace Endatix.Core.Features.Email;

public abstract class BaseEmailModel : IHasMetadata
{
    public required string To { get; init; }

    public required string From { get; init; }

    public required string Subject { get; init; }

    public Dictionary<string, object> Metadata { get; init; } = [];

    public IHasMetadata MetadataOperations => this;
}
