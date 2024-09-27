using Endatix.Core.Abstractions;
using Microsoft.EntityFrameworkCore.ValueGeneration;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Endantix.Infrastructure.Data;

/// <summary>
/// This class is a custom value generator for EF Core, specifically designed to work with Snowflake identifiers.
/// It utilizes an instance of <see cref="IIdGenerator{long}"/> to generate unique identifiers.
/// </summary>
public class SnowflakeValueGenerator : ValueGenerator<long>
{
    private readonly IIdGenerator<long> _idGenerator;

    /// <summary>
    /// Initializes a new instance of the <see cref="SnowflakeValueGenerator"/> class.
    /// </summary>
    /// <param name="idGenerator">An instance of <see cref="IIdGenerator{long}"/> used for generating unique identifiers.</param>
    public SnowflakeValueGenerator(IIdGenerator<long> idGenerator)
    {
        _idGenerator = idGenerator;
    }

    /// <summary>
    /// Generates a unique identifier for the given entity entry.
    /// </summary>
    /// <inheritdoc/>
    /// <param name="entry">The entity entry for which a unique identifier is being generated.</param>
    /// <returns>A unique identifier as a long.</returns>
    public override long Next(EntityEntry entry)
    {
        return _idGenerator.CreateId();
    }

    /// <inheritdoc/>
    public override bool GeneratesTemporaryValues => false;
}