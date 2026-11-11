using Endatix.Core.Abstractions;
using Microsoft.Extensions.Options;
using Snowflake = IdGen;

namespace Endatix.Infrastructure.Data;

/// <summary>
/// This class implements the IIdGenerator interface to generate unique identifiers.
/// It utilizes the Snowflake IdGenerator by <see href="https://github.com/RobThree/IdGen"/> for generating unique identifiers.
/// </summary>
public class SnowflakeIdGenerator : IIdGenerator<long>
{
    private readonly Snowflake.IdGenerator _generator;

    public SnowflakeIdGenerator(IOptions<DataOptions> options)
        : this(options.Value.SnowflakeWorkerId)
    {
    }

    internal SnowflakeIdGenerator(int workerId)
    {
        _generator = SnowflakeIdGenerators.For(workerId);
    }

    /// <summary>
    /// Generates a unique identifier using the Snowflake IdGenerator.
    /// </summary>
    /// <returns>A unique identifier as long.</returns>
    public long CreateId() => _generator.CreateId();
}
