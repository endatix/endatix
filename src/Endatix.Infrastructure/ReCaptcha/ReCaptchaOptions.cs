using Endatix.Framework.Configuration;

namespace Endatix.Infrastructure.ReCaptcha;

public sealed class ReCaptchaOptions : EndatixOptionsBase
{
    public override string SectionPath => "ReCaptcha";

    /// <summary>
    /// The secret key for reCAPTCHA verification
    /// </summary>
    public string SecretKey { get; init; } = string.Empty;

    /// <summary>
    /// Minimum score threshold for reCAPTCHA verification (0.0 to 1.0)
    /// </summary>
    public double MinimumScore { get; init; } = 0.5;

    /// <summary>
    /// Whether reCAPTCHA verification is globally enabled
    /// </summary>
    public bool IsEnabled { get; init; } = false;

    /// <summary>
    /// List of tenant IDs for which reCAPTCHA is enabled
    /// </summary>
    public IReadOnlyCollection<long>? EnabledForTenantIds { get; init; }
}