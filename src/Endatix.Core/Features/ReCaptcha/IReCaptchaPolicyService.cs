using Endatix.Core.Entities;

namespace Endatix.Core.Features.ReCaptcha;

/// <summary>
/// Service for validating reCAPTCHA tokens and apply reCAPTCHA Policy to forms
/// </summary>
public interface IReCaptchaPolicyService
{
    /// <summary>
    /// Verifies a reCAPTCHA token with Google's API
    /// </summary>
    /// <param name="token">The reCAPTCHA token to verify</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result indicating if the token is valid</returns>
    Task<ReCaptchaVerificationResult> VerifyTokenAsync(string token, CancellationToken cancellationToken);

    /// <summary>
    /// Checks if reCAPTCHA is enabled
    /// </summary>
    /// <returns>True if reCAPTCHA is enabled, false otherwise</returns>
    bool IsEnabled { get; }

    /// <summary>
    /// Checks if reCAPTCHA is required for a given form
    /// </summary>
    /// <param name="form">The form ID to check</param>
    /// <returns>True if reCAPTCHA is required for the form, false otherwise</returns>
    bool RequiresReCaptcha(Form form);
}