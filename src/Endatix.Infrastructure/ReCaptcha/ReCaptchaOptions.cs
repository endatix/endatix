using Endatix.Framework.Configuration;

namespace Endatix.Infrastructure.ReCaptcha;

public sealed class ReCaptchaOptions : EndatixOptionsBase
{
    public override string SectionPath => "ReCaptcha";

    public string SecretKey { get; init; } = string.Empty;
    public double MinimumScore { get; init; } = 0.5;
    public bool IsEnabled { get; init; } = false;
}