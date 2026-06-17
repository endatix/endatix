namespace Endatix.Core.Features.Auth;

/// <summary>
/// Reads a server-safe snapshot of API authentication settings.
/// </summary>
public interface IAuthSettingsReader
{
    AuthSettingsDto GetSettings();
}
