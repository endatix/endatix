using Endatix.Core.Branding;
using Endatix.Core.Configuration;

namespace Endatix.Core.Features.Email;

/// <summary>
/// The deployment's resolved sender.
/// </summary>
/// <param name="Address">The sender of an email that has no template and no From address.</param>
/// <param name="Name">The From display name of every email.</param>
/// <param name="ConfiguredAddress">
/// <c>Endatix:Integrations:Email:Sender:Address</c> when it is set. It wins over a template row's address. Null when
/// unset, so each template sends from its row.
/// </param>
public sealed record DeploymentSender(string Address, string Name, string? ConfiguredAddress)
{
    /// <summary>
    /// Resolves the sender. The settings win, then the deprecated <paramref name="legacy"/> values
    /// (<c>SmtpSettings:DefaultFromAddress</c> and <c>DefaultFromName</c>), then the default address and the brand's
    /// product name. A deprecated address is not a configured address: it never wins over a template row, as before.
    /// </summary>
    public static DeploymentSender From(EmailSenderSettings settings, DeploymentBrand brand, EmailSenderSettings? legacy = null)
    {
        var configuredAddress = Value(settings.Address);
        return new DeploymentSender(
            Address: configuredAddress ?? Value(legacy?.Address) ?? EmailSenderSettings.DefaultAddress,
            Name: Value(settings.Name) ?? Value(legacy?.Name) ?? brand.ProductName,
            ConfiguredAddress: configuredAddress);
    }

    private static string? Value(string? setting) => string.IsNullOrWhiteSpace(setting) ? null : setting;
}
