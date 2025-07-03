namespace Endatix.Infrastructure.ReCaptcha;

/// <summary>
/// Service for validating Google reCAPTCHA v3 tokens
/// </summary>
public interface IGoogleReCaptchaService
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
}