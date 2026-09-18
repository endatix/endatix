using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.ValueGeneration;

namespace Endatix.Infrastructure.Data;

/// <summary>
/// EF factory for <see cref="SnowflakeValueGenerator"/>. Construction only — no <c>IIdGenerator</c>
/// here (the model is cached per context type). Ids are resolved in <see cref="SnowflakeValueGenerator.Next"/>.
/// </summary>
public sealed class SnowflakeValueGeneratorFactory : ValueGeneratorFactory
{
    public override ValueGenerator Create(IProperty property, ITypeBase typeBase) =>
        new SnowflakeValueGenerator();
}
