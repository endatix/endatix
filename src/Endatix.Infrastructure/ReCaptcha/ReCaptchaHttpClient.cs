using System.Text.Json;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Infrastructure.ReCaptcha;

public class ReCaptchaHttpClient(HttpClient client) : IReCaptchaHttpClient
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
                catch (Exception)
                {
                    return Result.Error("Failed to deserialize Google ReCaptcha response");
                }
            }

            return Result.Error("Failed to validate reCAPTCHA token");
        }
        catch (Exception ex)
        {
            return Result.Error($"Http error during token validation: {ex.Message}");
        }
    }
}