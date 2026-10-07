namespace Endatix.Api.Endpoints.Submissions;

/// <summary>
/// Base class for public (anonymous) submission request models that require bot protection
/// </summary>
public abstract class BasePublicSubmissionRequest : BaseSubmissionRequest
{
    /// <summary>
    /// reCAPTCHA v3 token for bot protection
    /// </summary>
    public string? ReCaptchaToken { get; set; }

    /// <summary>
    /// <c>screen_out</c> when a screen-out trigger ends the interview. The submission stays
    /// incomplete and later updates are rejected. Omit for a normal save. Any other value is rejected.
    /// Respondent saves only: staff endpoints do not accept it.
    /// </summary>
    public string? CollectionOutcome { get; set; }
}
