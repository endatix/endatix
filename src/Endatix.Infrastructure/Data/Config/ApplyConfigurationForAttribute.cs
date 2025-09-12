using Microsoft.EntityFrameworkCore;

namespace Endatix.Infrastructure.Data.Config;

/// <summary>
/// Attribute to mark entity configurations for specific DbContext types.
/// This allows for applying configurations to different DbContexts that share the same assembly.
/// This attribute can only be applied to classes that implement IEntityTypeConfiguration&lt;T&gt;.
/// </summary>
/// <typeparam name="TDbContext">The DbContext type this configuration applies to</typeparam>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public class ApplyConfigurationForAttribute<TDbContext> : Attribute where TDbContext : DbContext
{
    public Type DbContextType => typeof(TDbContext);
}