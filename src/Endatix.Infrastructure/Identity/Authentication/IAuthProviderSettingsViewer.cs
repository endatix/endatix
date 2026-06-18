using Endatix.Core.Features.Auth;
using Microsoft.Extensions.Configuration;

namespace Endatix.Infrastructure.Identity.Authentication;

/// <summary>
/// Optional hook for auth providers to supply admin-safe settings views.
/// Implement on <see cref="IAuthProvider"/> to avoid central type switches in
/// <see cref="AuthSettingsReader"/>.
/// </summary>
public interface IAuthProviderSettingsViewer
{
    /// <summary>
    /// Projects provider configuration into an admin-safe view. Must not expose secret values.
    /// </summary>
    AuthProviderSettingsDto ViewSettings(
        AuthProviderSettingsDto baseline,
        IConfigurationSection section,
        IList<string> configurationErrors);
}
