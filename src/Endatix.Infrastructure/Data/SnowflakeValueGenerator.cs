using Endatix.Core.Abstractions;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.ValueGeneration;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.Infrastructure.Data;

public sealed class SnowflakeValueGenerator : ValueGenerator<long>
{
    private static readonly IIdGenerator<long> Fallback = new SnowflakeIdGenerator();

    public override bool GeneratesTemporaryValues => false;

    public override long Next(EntityEntry entry)
    {
        // Host DI (AddDbContext), not context.GetService — that is EF's internal provider.
        var appServices = entry.Context.GetService<IDbContextOptions>()
            .FindExtension<CoreOptionsExtension>()
            ?.ApplicationServiceProvider;

        var idGenerator = appServices?.GetService<IIdGenerator<long>>() ?? Fallback;
        return idGenerator.CreateId();
    }
}
