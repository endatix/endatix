using IdGen;
using Microsoft.Extensions.Logging;
using Endatix.Core.Abstractions;
using Endatix.Core.Configuration;

namespace Endatix.Infrastructure.Services;


public class SnowflakeIdGenerator(ILogger<SnowflakeIdGenerator> logger) : IIdGenerator
{
    private static readonly IdGenerator _idGenerator = new IdGenerator(EndatixConfig.Configuration.SnowflakeGeneratorId);

    private readonly ILogger _logger = logger;

    public long CreateId()
    {
        return _idGenerator.CreateId();
    }
}
