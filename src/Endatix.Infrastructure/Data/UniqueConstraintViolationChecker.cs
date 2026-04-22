using Endatix.Core.Abstractions.Data;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Infrastructure.Data;

internal sealed class UniqueConstraintViolationChecker : IUniqueConstraintViolationChecker
{
    private const string POSTGRES_UNIQUE_VIOLATION = "23505";
    private static readonly int[] _sqlServerUniqueViolationCodes = [2601, 2627];

    /// <summary>
    /// Checks if the exception is a unique constraint violation.
    /// </summary>
    /// <param name="exception">The exception to check.</param>
    /// <returns>True if the exception is a unique constraint violation, false otherwise.</returns>
    public bool IsUniqueConstraintViolation(Exception exception)
    {
        if (exception is not DbUpdateException dbUpdateException)
        {
            return false;
        }

        var current = dbUpdateException.InnerException ?? dbUpdateException;
        while (current is not null)
        {
            if (IsPostgresUniqueViolation(current) || IsSqlServerUniqueViolation(current))
            {
                return true;
            }

            current = current.InnerException;
        }

        return false;
    }

    /// <summary>
    /// Checks if the exception is a PostgreSQL unique constraint violation.
    /// </summary>
    /// <param name="exception">The exception to check.</param>
    /// <returns>True if the exception is a PostgreSQL unique constraint violation, false otherwise.</returns>
    private static bool IsPostgresUniqueViolation(Exception exception)
    {
        if (exception.GetType().FullName != "Npgsql.PostgresException")
        {
            return false;
        }

        var sqlState = GetStringProperty(exception, "SqlState");
        if (!string.Equals(sqlState, POSTGRES_UNIQUE_VIOLATION, StringComparison.Ordinal))
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Checks if the exception is a SQL Server unique constraint violation.
    /// </summary>
    /// <param name="exception">The exception to check.</param>
    /// <returns>True if the exception is a SQL Server unique constraint violation, false otherwise.</returns>
    private static bool IsSqlServerUniqueViolation(Exception exception)
    {
        var fullName = exception.GetType().FullName;
        if (fullName != "Microsoft.Data.SqlClient.SqlException" && fullName != "System.Data.SqlClient.SqlException")
        {
            return false;
        }

        var number = GetIntProperty(exception, "Number");
        return number.HasValue && _sqlServerUniqueViolationCodes.Contains(number.Value);
    }

    private static string? GetStringProperty(object source, string propertyName) =>
        source.GetType().GetProperty(propertyName)?.GetValue(source) as string;

    private static int? GetIntProperty(object source, string propertyName)
    {
        var value = source.GetType().GetProperty(propertyName)?.GetValue(source);
        return value is int number ? number : null;
    }
}
