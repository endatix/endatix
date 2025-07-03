using Endatix.Infrastructure.ReCaptcha;

namespace Endatix.Api.Endpoints.Submissions;

/// <summary>
/// Base class for submission request models with common properties
/// </summary>
public abstract class BaseSubmissionRequest
{
    /// <summary>
    /// The ID of the form for which the submission is made.
    /// </summary>
    public long FormId { get; set; }

    /// <summary>
    /// Boolean flag to indicate if a submission is complete. Optional
    /// </summary>
    public bool? IsComplete { get; set; }

    /// <summary>
    /// Current page if the form has multiple pages. Optional
    /// </summary>
    public int? CurrentPage { get; set; }

    /// <summary>
    /// Stringified form submission data
    /// </summary>
    public string? JsonData { get; set; }

    /// <summary>
    /// Stringified metadata related to the form submission
    /// </summary>
    public string? Metadata { get; set; }

    /// <summary>
    /// reCAPTCHA v3 token for bot protection
    /// </summary>
    public string? ReCaptchaToken { get; set; }

    /// <summary>
    /// Validates the reCAPTCHA token if it is present and the form is complete
    /// </summary>
    /// <param name="recaptchaService">The reCAPTCHA service to use</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The result of the reCAPTCHA verification <see cref="ReCaptchaVerificationResult"/></returns>
    public async Task<ReCaptchaVerificationResult> ValidateReCaptchaAsync(
        IGoogleReCaptchaService recaptchaService,
        CancellationToken cancellationToken)
    {
        var shouldValidate =
            recaptchaService.IsEnabled
            && string.IsNullOrEmpty(ReCaptchaToken) == false
            && IsComplete == true;

        if (!shouldValidate)
        {
            return ReCaptchaVerificationResult.Skipped();
        }

        return await recaptchaService.VerifyTokenAsync(ReCaptchaToken!, cancellationToken);
    }
}