using Microsoft.EntityFrameworkCore;

namespace Endatix.Infrastructure.Data;

/// <summary>
/// Provider-specific options every module <see cref="DbContext"/> gets, such as the Endatix database functions the
/// shared query helpers translate to (<see cref="Querying.IRelationalJsonObjectKeyFilter"/>).
/// </summary>
/// <remarks>
/// Registered by the persistence provider, because Infrastructure and the modules do not reference it. Applied by
/// <see cref="ModuleDbContextExtensions.AddModuleDbContext{TContext}"/> when the context is created.
/// </remarks>
public interface IModuleDbContextProviderOptions
{
    /// <summary>Adds the provider's options to a module context being configured.</summary>
    /// <param name="optionsBuilder">The module context's options builder.</param>
    void Apply(DbContextOptionsBuilder optionsBuilder);
}
