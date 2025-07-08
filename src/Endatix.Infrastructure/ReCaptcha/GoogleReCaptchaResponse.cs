using System.Text.Json.Serialization;

namespace Endatix.Infrastructure.ReCaptcha;

public sealed record GoogleReCaptchaResponse(
    [property: JsonPropertyName("success")] bool Success,
    [property: JsonPropertyName("challenge_ts")] DateTime ChallengeTs,
    [property: JsonPropertyName("hostname")] string Hostname,
    [property: JsonPropertyName("score")] double Score,
    [property: JsonPropertyName("action")] string Action,
    [property: JsonPropertyName("error-codes")] string[]? ErrorCodes
);