using System.Security.Claims;

namespace Endatix.Core.Infrastructure.Logging;

/// <summary>
/// Converts MediatR request property values into log-safe representations.
/// Prevents Serilog destructuring from expanding circular graphs such as <see cref="ClaimsPrincipal"/>.
/// </summary>
internal static class LogPropertyFormatter
{
    public static object? FormatForLog(object? value)
    {
        if (value is null)
        {
            return null;
        }

        if (IsSimpleType(value.GetType()))
        {
            return value;
        }

        return value switch
        {
            ClaimsPrincipal principal => FormatClaimsPrincipal(principal),
            _ => $"[{value.GetType().Name}]"
        };
    }

    private static string FormatClaimsPrincipal(ClaimsPrincipal principal)
    {
        var isAuthenticated = principal.Identity?.IsAuthenticated == true;
        var userId = principal.FindFirst("sub")?.Value
            ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? "unknown";

        return $"ClaimsPrincipal(authenticated={isAuthenticated}, userId={userId})";
    }

    private static bool IsSimpleType(Type type)
    {
        if (type.IsPrimitive || type.IsEnum)
        {
            return true;
        }

        return type == typeof(string)
            || type == typeof(decimal)
            || type == typeof(DateTime)
            || type == typeof(DateTimeOffset)
            || type == typeof(TimeSpan)
            || type == typeof(Guid);
    }
}
