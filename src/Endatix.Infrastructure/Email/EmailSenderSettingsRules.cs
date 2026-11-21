using System.Net.Mail;
using Endatix.Core.Configuration;

namespace Endatix.Infrastructure.Email;

/// <summary>
/// The startup check of <see cref="EmailSenderSettings"/>. Both values reach the From header, so the address must be
/// bare (a display name belongs in <see cref="EmailSenderSettings.Name"/>) and the name must hold no control
/// characters.
/// </summary>
internal static class EmailSenderSettingsRules
{
    internal const string Message =
        $"{EmailSenderSettings.SectionName}: Address must be a bare email address and Name must not contain control characters.";

    internal static bool AreValid(EmailSenderSettings settings) =>
        IsBareAddress(settings.Address) && !(settings.Name ?? string.Empty).Any(char.IsControl);

    // Empty falls back to the default address.
    private static bool IsBareAddress(string? address) =>
        string.IsNullOrWhiteSpace(address)
        || (MailAddress.TryCreate(address, out var parsed) && parsed.Address == address);
}
