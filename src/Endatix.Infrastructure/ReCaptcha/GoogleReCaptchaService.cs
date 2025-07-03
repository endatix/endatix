using System.Text.Json;
using Microsoft.Extensions.Options;
using System.Text.Json.Serialization;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Infrastructure.ReCaptcha;

internal sealed class GoogleReCaptchaService : IGoogleReCaptchaService
{
    private readonly IReCaptchaHttpClient _httpClient;
    private readonly ReCaptchaOptions _options;

    public GoogleReCaptchaService(IReCaptchaHttpClient httpClient, IOptions<ReCaptchaOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
        IsEnabled = _options.IsEnabled
            && !string.IsNullOrEmpty(_options.SecretKey)
            && _options.MinimumScore > 0.0
            && _options.MinimumScore <= 1.0;
    }

    public async Task<ReCaptchaVerificationResult> VerifyTokenAsync(string token, CancellationToken cancellationToken)
    {
        if (!IsEnabled)
        {
            return ReCaptchaVerificationResult.NotEnabled();
        }

        if (string.IsNullOrEmpty(token))
        {
            return ReCaptchaVerificationResult.InvalidResponse(0.0, "N/A", "token_missing");
        }

        var tokenValidationResponse = await GetTokenValidationResponseAsync(token, cancellationToken);

        if (!tokenValidationResponse.IsSuccess)
        {
            return ReCaptchaVerificationResult.InvalidResponse(0.0, "N/A", ["invalid_response"]);
        }

        var result = tokenValidationResponse.Value;

        if (!result.Success)
        {
            return ReCaptchaVerificationResult.InvalidResponse(0.0, result.Action, result.ErrorCodes ?? []);
        }

        if (result.Score < _options.MinimumScore)
        {
            return ReCaptchaVerificationResult.InvalidResponse(result.Score, result.Action, ["score_too_low", $"{result.Score} < {_options.MinimumScore}"]);
        }

        return ReCaptchaVerificationResult.Success(result.Score, result.Action);
    }

    private async Task<Result<GoogleReCaptchaResponse>> GetTokenValidationResponseAsync(string token, CancellationToken cancellationToken)
    {
        var content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("secret", _options.SecretKey),
            new KeyValuePair<string, string>("response", token)
        });

        var response = await _httpClient.VerifyTokenAsync(content, cancellationToken);
        var responseContent = await response!.Content.ReadAsStreamAsync(cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            try
            {
                var result = await JsonSerializer.DeserializeAsync<GoogleReCaptchaResponse>(responseContent, cancellationToken: cancellationToken);
                return result is null ?
                Result.Error("Failed to deserialize Google ReCaptcha response") :
                Result.Success(result);
            }
            catch (Exception)
            {
                return Result.Error("Failed to deserialize Google ReCaptcha response");
            }
        }

        return Result.Error("Failed to validate reCAPTCHA token");
    }

    public bool IsEnabled { get; }

    private sealed record GoogleReCaptchaResponse(
        [property: JsonPropertyName("success")] bool Success,
        [property: JsonPropertyName("challenge_ts")] DateTime ChallengeTs,
        [property: JsonPropertyName("hostname")] string Hostname,
        [property: JsonPropertyName("score")] double Score,
        [property: JsonPropertyName("action")] string Action,
        [property: JsonPropertyName("error-codes")] string[]? ErrorCodes
    );
}