using System.Text.Json.Serialization;

namespace Endatix.Infrastructure.ReCaptcha;

/// <summary>
/// Google siteverify payload. <c>hostname</c> and <c>action</c> are absent on most
/// <c>success: false</c> responses, so they are nullable.
/// </summary>
public sealed record GoogleReCaptchaResponse(
    [property: JsonPropertyName("success")] bool Success,
    [property: JsonPropertyName("challenge_ts")] DateTime ChallengeTs,
    [property: JsonPropertyName("hostname")] string? Hostname,
    [property: JsonPropertyName("score")] double Score,
    [property: JsonPropertyName("action")] string? Action,
    [property: JsonPropertyName("error-codes")] string[]? ErrorCodes
);