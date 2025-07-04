using Endatix.Core.Entities;

namespace Endatix.Core.Features.ReCaptcha;

/// <summary>
/// Context for verifying a submission with reCAPTCHA.
/// </summary>
/// <param name="Form"></param>
/// <param name="IsComplete"></param>
/// <param name="JsonData"></param>
/// <param name="ReCaptchaToken"></param>
public sealed record SubmissionVerificationContext(
    Form Form,
    bool IsComplete,
    string? JsonData,
    string? ReCaptchaToken
);