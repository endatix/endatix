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
                    return result is null ?
                    Result.Error("Failed to deserialize Google ReCaptcha response") :
                    Result.Success(result);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to deserialize Google ReCaptcha response");
                    return Result.Error("Failed to deserialize Google ReCaptcha response");
                }
            }

            return Result.Error("Failed to validate reCAPTCHA token");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "HTTP error during reCAPTCHA token validation");
            return Result.Error("Failed to validate reCAPTCHA token");
        }
    }
}