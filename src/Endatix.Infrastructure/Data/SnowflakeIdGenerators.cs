using System.Collections.Concurrent;
using Snowflake = IdGen;

namespace Endatix.Infrastructure.Data;

/// <summary>
/// One <see cref="Snowflake.IdGenerator"/> per worker id for this process.
/// Two generators with the same worker id share one sequence, so they cannot mint the same id.
/// </summary>
internal static class SnowflakeIdGenerators
{
    private static readonly ConcurrentDictionary<int, Snowflake.IdGenerator> _byWorker = new();

    public static Snowflake.IdGenerator For(int workerId) =>
        _byWorker.GetOrAdd(workerId, static id => new Snowflake.IdGenerator(id));
}
