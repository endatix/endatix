namespace Endatix.Core.UseCases.Email.GetSettings;

/// <summary>
/// DTO for the active email sender settings.
/// </summary>
/// <param name="ProviderName">The name of the active email provider.</param>
/// <param name="IsConfigured">Whether the active email provider is configured.</param>
public record EmailSettingsDto(string ProviderName, bool IsConfigured);
