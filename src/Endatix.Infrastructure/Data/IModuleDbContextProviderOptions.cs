using Microsoft.EntityFrameworkCore;

namespace Endatix.Infrastructure.Data;

/// <summary>
/// Provider-specific options every module <see cref="DbContext"/> gets, such as the Endatix database functions the
/// shared query helpers translate to (<see cref="Querying.IRelationalJsonObjectKeyFilter"/>).
/// </summary>
public interface IModuleDbContextProviderOptions
{
    /// <summary>Adds the provider's options to a module context being configured.</summary>
    /// <param name="optionsBuilder">The module context's options builder.</param>
    void Apply(DbContextOptionsBuilder optionsBuilder);
}
