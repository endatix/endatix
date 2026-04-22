namespace Endatix.Infrastructure.Data.Config;

/// <summary>
/// Optional contract for <see cref="Microsoft.EntityFrameworkCore.IEntityTypeConfiguration{TEntity}"/> types that need
/// the active EF Core database provider name (the same value exposed as <c>DbContext.Database.ProviderName</c>)
/// when building the model (for example, provider-specific index filters).
/// Compare the name passed to <see cref="SetDatabaseProviderName"/> using <see cref="EfCoreDatabaseProviders"/>.
/// </summary>
public interface IDatabaseProviderAwareConfiguration
{
    /// <summary>
    /// Called once after parameterless construction when the host passes the active EF Core provider name into the model builder pipeline.
    /// </summary>
    /// <param name="databaseProviderName">The provider name, or <see langword="null"/> when no context was passed or the provider is unknown.</param>
    void SetDatabaseProviderName(string? databaseProviderName);
}
