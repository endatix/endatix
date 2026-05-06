using System.Collections.Concurrent;
using System.Reflection;
using System.Text.RegularExpressions;
using Endatix.Core.Abstractions.Data;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Infrastructure.Data;

internal sealed class UniqueConstraintViolationChecker : IUniqueConstraintViolationChecker
{
    private const string POSTGRES_UNIQUE_VIOLATION = "23505";
    private static readonly ConcurrentDictionary<(Type Type, string PropertyName), PropertyInfo?> _propertyCache = new();
    private static readonly Regex _sqlServerConstraintRegex = new(
        @"constraint\s+'(?<constraint>[^']+)'",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    /// <inheritdoc />
    public UniqueConstraintViolationResult AnalyzeUniqueConstraint(Exception exception)
    {
        if (exception is not DbUpdateException dbUpdateException)
        {
            return new UniqueConstraintViolationResult(false, null, null);
        }

        var current = dbUpdateException.InnerException ?? dbUpdateException;
        while (current is not null)
        {
            if (IsPostgresUniqueViolation(current))
            {
                return new UniqueConstraintViolationResult(
                    true,
                    ReadConstraintName(current) ?? ExtractSqlServerConstraintNameFromMessage(current.Message),
                    ReadColumnName(current));
            }

            if (IsSqlServerUniqueViolation(current))
            {
                return new UniqueConstraintViolationResult(
                    true,
                    ReadConstraintName(current) ?? ExtractSqlServerConstraintNameFromMessage(current.Message),
                    ReadColumnName(current));
            }

            current = current.InnerException;
        }

        return new UniqueConstraintViolationResult(false, null, null);
    }

    private static string? ReadConstraintName(Exception exception)
    {
        var direct = TryGetPropertyString(exception, "ConstraintName");
        if (!string.IsNullOrEmpty(direct))
        {
            return direct;
        }

        var errors = GetCachedProperty(exception.GetType(), "Errors");
        if (errors?.GetValue(exception) is IEnumerable<object> collection)
        {
            foreach (var item in collection)
            {
                var fromError = TryGetPropertyString(item, "ConstraintName");
                if (!string.IsNullOrEmpty(fromError))
                {
                    return fromError;
                }
            }
        }

        return null;
    }

    private static string? ReadColumnName(Exception exception) =>
        TryGetPropertyString(exception, "ColumnName");

    private static string? ExtractSqlServerConstraintNameFromMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return null;
        }

        var match = _sqlServerConstraintRegex.Match(message);
        if (!match.Success)
        {
            return null;
        }

        var captured = match.Groups["constraint"].Value;
        return string.IsNullOrWhiteSpace(captured) ? null : captured;
    }

    private static string? TryGetPropertyString(object source, string propertyName)
    {
        var property = GetCachedProperty(source.GetType(), propertyName);
        return property?.GetValue(source) as string;
    }

    private static bool IsPostgresUniqueViolation(Exception exception)
    {
        if (exception.GetType().FullName != "Npgsql.PostgresException")
        {
            return false;
        }

        var sqlState = TryGetPropertyString(exception, "SqlState");
        if (!string.Equals(sqlState, POSTGRES_UNIQUE_VIOLATION, StringComparison.Ordinal))
        {
            return false;
        }

        return true;
    }

    private static bool IsSqlServerUniqueViolation(Exception exception)
    {
        var fullName = exception.GetType().FullName;
        if (fullName != "Microsoft.Data.SqlClient.SqlException" && fullName != "System.Data.SqlClient.SqlException")
        {
            return false;
        }

        var number = GetIntProperty(exception, "Number");
        return number is 2601 or 2627;
    }

    private static int? GetIntProperty(object source, string propertyName)
    {
        var property = GetCachedProperty(source.GetType(), propertyName);
        var value = property?.GetValue(source);
        return value is int number ? number : null;
    }

    private static PropertyInfo? GetCachedProperty(Type type, string propertyName) =>
        _propertyCache.GetOrAdd(
            (type, propertyName),
            static key => key.Type.GetProperty(key.PropertyName, BindingFlags.Public | BindingFlags.Instance));
}
