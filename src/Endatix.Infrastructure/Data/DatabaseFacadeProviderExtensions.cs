using Ardalis.GuardClauses;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Endatix.Infrastructure.Data;

/// <summary>
/// Stops a context that is mapped for one database provider from building its model for another.
/// </summary>
/// <remarks>
/// A module's derived context maps provider-specific column types and index SQL. Built for the wrong
/// provider, its model still builds and the first query fails with an unrelated type or syntax error, so
/// the module's base context calls one of these, through its abstract <c>EnsureSupportedProvider</c>, before
/// it applies any mapping. This checks a built context; <see cref="DatabaseProviderResolver"/> checks the
/// configuration when the module registers.
/// </remarks>
public static class DatabaseFacadeProviderExtensions
{
    /// <summary>Throws unless <paramref name="database"/> is configured for PostgreSQL (Npgsql).</summary>
    /// <param name="database">The context's <see cref="DbContext.Database"/>.</param>
    /// <param name="contextType">The context that requires PostgreSQL, named in the exception.</param>
    /// <exception cref="NotSupportedException">The context is configured for another provider.</exception>
    public static void EnsureNpgsql(this DatabaseFacade database, Type contextType)
    {
        Guard.Against.Null(database);
        Guard.Against.Null(contextType);

        if (!database.IsNpgsql())
        {
            throw WrongProvider(database, contextType, "PostgreSQL");
        }
    }

    /// <summary>Throws unless <paramref name="database"/> is configured for SQL Server.</summary>
    /// <param name="database">The context's <see cref="DbContext.Database"/>.</param>
    /// <param name="contextType">The context that requires SQL Server, named in the exception.</param>
    /// <exception cref="NotSupportedException">The context is configured for another provider.</exception>
    public static void EnsureSqlServer(this DatabaseFacade database, Type contextType)
    {
        Guard.Against.Null(database);
        Guard.Against.Null(contextType);

        if (!database.IsSqlServer())
        {
            throw WrongProvider(database, contextType, "SQL Server");
        }
    }

    private static NotSupportedException WrongProvider(
        DatabaseFacade database,
        Type contextType,
        string requiredProvider) =>
        new($"{contextType.Name} is mapped for {requiredProvider} only, but it is configured for the " +
            $"'{database.ProviderName}' provider. Register the context that matches the configured " +
            "database provider.");
}
