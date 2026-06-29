using System.Text.Json;
using Endatix.Core.Abstractions.Authorization;

namespace Endatix.Infrastructure.Features.PlatformAdmin.Common;

/// <summary>
/// Reads external role claims from persisted JSON without exposing raw payloads to API consumers.
/// </summary>
internal static class PlatformAdminExternalRoleReader
{
    /// <summary>
    /// JSON string-array element marker used only for SQL sort heuristics (e.g. <c>["PlatformAdmin"]</c>).
    /// Display flags use <see cref="HasPlatformAdminRole"/>.
    /// </summary>
    internal static string QuotedPlatformAdminRoleName { get; } =
        $"\"{SystemRole.PlatformAdmin.Name}\"";

    /// <summary>
    /// Checks if the platform administrator role is present in the external roles JSON.
    /// </summary>
    /// <param name="externalRolesJson">The external roles JSON.</param>
    /// <returns>True if the platform administrator role is present, false otherwise.</returns>
    internal static bool HasPlatformAdminRole(string? externalRolesJson)
    {
        if (string.IsNullOrWhiteSpace(externalRolesJson))
        {
            return false;
        }

        try
        {
            var roles = JsonSerializer.Deserialize<string[]>(externalRolesJson);
            return roles?.Any(SystemRole.IsPlatformAdminRoleName) == true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
