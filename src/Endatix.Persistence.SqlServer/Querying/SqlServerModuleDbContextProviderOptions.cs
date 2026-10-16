using Endatix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Endatix.Persistence.SqlServer.Querying;

/// <summary>
/// Gives module contexts the same SQL Server database functions as the core context.
/// </summary>
internal sealed class SqlServerModuleDbContextProviderOptions : IModuleDbContextProviderOptions
{
    public void Apply(DbContextOptionsBuilder optionsBuilder) =>
        optionsBuilder.ReplaceService<IModelCustomizer, SqlServerEndatixModelCustomizer>();
}
