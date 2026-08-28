using System.Text.Json;
using Endatix.Core.Infrastructure.Result;
using Microsoft.Extensions.Logging;

namespace Endatix.Infrastructure.ReCaptcha;

public class ReCaptchaHttpClient(HttpClient client, ILogger<ReCaptchaHttpClient> logger) : IReCaptchaHttpClient
{
    public async Task<Result<GoogleReCaptchaResponse>> GetTokenValidationResponseAsync(string token, string secretKey, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(token))
        {
            return Result.Error("Token is required");
        }

        if (string.IsNullOrEmpty(secretKey))
        {
            return Result.Error("Secret key is required");
        }

        var content = new FormUrlEncodedContent(new[]
            {
            new KeyValuePair<string, string>("secret", secretKey),
            new KeyValuePair<string, string>("response", token)
        });
        try
        {
            var response = await client.PostAsync("https://www.google.com/recaptcha/api/siteverify", content, cancellationToken);

            var responseContent = await response!.Content.ReadAsStreamAsync(cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                try
                {
                    var result = await JsonSerializer.DeserializeAsync<GoogleReCaptchaResponse>(responseContent, cancellationToken: cancellationToken);
                    if (result is null)
                    {
                        logger.LogError("Google ReCaptcha returned a success status with a null response body");
                        return Result.Error("Failed to deserialize Google ReCaptcha response");
                    }

                    return Result.Success(result);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to deserialize Google ReCaptcha response");
                    return Result.Error("Failed to deserialize Google ReCaptcha response");
                }
            }

            // Distinct from the transport failure below: Google answered, and it refused the request
            // (bad secret key, malformed form, quota). The status code is diagnostic for us, not for the
            // caller, so it is logged and kept out of the returned message.
            logger.LogError(
                "reCAPTCHA verification returned {StatusCode} for the siteverify request",
                (int)response.StatusCode);
            return Result.Error("The reCAPTCHA verification service rejected the request.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "HTTP error during reCAPTCHA token validation");
            return Result.Error("Could not reach the reCAPTCHA verification service.");
        }
    }
}