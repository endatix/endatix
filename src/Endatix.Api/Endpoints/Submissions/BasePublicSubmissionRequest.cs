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
    /// A closed collection status that ends the interview, e.g. <c>screen_out</c> or a closed code the host registered.
    /// The submission stays incomplete and later updates are rejected. Omit for a normal save. Any other value is
    /// rejected, including <c>quota_full</c> and <c>cancelled</c>, which only the server sets. Respondent saves only:
    /// staff endpoints do not accept it.
    /// </summary>
    public string? CollectionOutcome { get; set; }
}
