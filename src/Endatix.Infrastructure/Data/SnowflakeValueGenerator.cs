using Endatix.Core.Abstractions;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.ValueGeneration;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.Infrastructure.Data;

public sealed class SnowflakeValueGenerator : ValueGenerator<long>
{
    // Design-time only (EF tooling builds a context with no application services). Runtime resolves the DI singleton.
    private static readonly IIdGenerator<long> _designTimeFallback = new SnowflakeIdGenerator(workerId: 0);

    public override bool GeneratesTemporaryValues => false;

    public override long Next(EntityEntry entry)
    {
        // Host DI (AddDbContext), not context.GetService — that is EF's internal provider.
        var appServices = entry.Context.GetService<IDbContextOptions>()
            .FindExtension<CoreOptionsExtension>()
            ?.ApplicationServiceProvider;

        var idGenerator = appServices?.GetService<IIdGenerator<long>>() ?? _designTimeFallback;
        return idGenerator.CreateId();
    }
}
