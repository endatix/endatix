using Endatix.Framework.Configuration;

namespace Endatix.Infrastructure.Identity.EmailVerification;

/// <summary>
/// Configuration options for email verification.
/// </summary>
public class EmailVerificationOptions : EndatixOptionsBase
{
    /// <summary>
    /// Gets the section path for these options.
    /// </summary>
    public override string SectionPath => "EmailVerification";

    /// <summary>
    /// Gets or sets the token expiry in hours.
    /// </summary>
    public int TokenExpiryInHours { get; set; } = 24;
} 