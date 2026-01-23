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
}
