
using Endatix.Core.Abstractions;
using Endatix.Core.Configuration;
using Snowflake = IdGen;

namespace Endatix.Infrastructure.Data;


/// <summary>
/// This class implements the IIdGenerator interface to generate unique identifiers.
/// It utilizes the Snowflake IdGenerator by <see href="https://github.com/RobThree/IdGen"/> for generating unique identifiers.
/// </summary>
public class SnowflakeIdGenerator : IIdGenerator<long>
{
    private static readonly Snowflake.IdGenerator _idGenerator = new Snowflake.IdGenerator(EndatixConfig.Configuration.SnowflakeGeneratorId);

    /// <summary>
    /// Generates a unique identifier using the Snowflake IdGenerator.
    /// </summary>
    /// <returns>A unique identifier as long.</returns>
    public long CreateId()
    {
        return _idGenerator.CreateId();
    }
}
