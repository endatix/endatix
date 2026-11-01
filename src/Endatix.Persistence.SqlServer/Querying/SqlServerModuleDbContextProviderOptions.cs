using Endatix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Endatix.Persistence.SqlServer.Querying;

internal sealed class SqlServerModuleDbContextProviderOptions : IModuleDbContextProviderOptions
{
    public void Apply(DbContextOptionsBuilder optionsBuilder) =>
        optionsBuilder.ReplaceService<IModelCustomizer, SqlServerEndatixModelCustomizer>();
}
