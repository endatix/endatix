using Endatix.Core.Branding;
using Endatix.Core.Configuration;

namespace Endatix.Core.Features.Email;

/// <summary>
/// The deployment's resolved sender: the configured <see cref="EmailSenderSettings"/>, with the brand's product name
/// as the display name when none is configured.
/// </summary>
/// <param name="Address">The sender address.</param>
/// <param name="Name">The From display name.</param>
public sealed record DeploymentSender(string Address, string Name)
{
    /// <summary>Resolves the sender from its settings and the deployment brand.</summary>
    public static DeploymentSender From(EmailSenderSettings settings, DeploymentBrand brand) => new(
        string.IsNullOrWhiteSpace(settings.Address) ? EmailSenderSettings.DefaultAddress : settings.Address,
        string.IsNullOrWhiteSpace(settings.Name) ? brand.ProductName : settings.Name);
}
