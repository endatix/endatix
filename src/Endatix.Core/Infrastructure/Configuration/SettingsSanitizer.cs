namespace Endatix.Core.Infrastructure.Configuration;

/// <summary>
/// Helpers for admin settings snapshots. Never return raw secret values from handlers.
/// </summary>
public static class SettingsSanitizer
{
    /// <summary>
    /// Returns whether a secret value is configured without exposing it.
    /// </summary>
    public static bool HasSecret(string? value) => !string.IsNullOrWhiteSpace(value);

    /// <summary>
    /// Masks a secret value for optional display contexts. Prefer presence flags in admin DTOs.
    /// </summary>
    public static string? MaskSecret(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        return new string('*', value.Length);
    }
}
