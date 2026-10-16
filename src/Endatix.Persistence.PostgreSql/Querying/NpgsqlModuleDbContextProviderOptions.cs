using Endatix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Endatix.Persistence.PostgreSql.Querying;

/// <summary>
/// Gives module contexts the same PostgreSQL database functions as the core context.
/// </summary>
internal sealed class NpgsqlModuleDbContextProviderOptions : IModuleDbContextProviderOptions
{
    public void Apply(DbContextOptionsBuilder optionsBuilder) =>
        optionsBuilder.ReplaceService<IModelCustomizer, NpgsqlEndatixModelCustomizer>();
}
