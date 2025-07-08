using Endatix.Core.Infrastructure.Result;

namespace Endatix.Infrastructure.ReCaptcha;

public interface IReCaptchaHttpClient
{
    /// <summary>
    /// Gets the token validation response from the Google ReCaptcha API
    /// </summary>
    /// <param name="token">The token to validate</param>
    /// <param name="secretKey">The secret key to use for the validation</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The token validation response if successful, otherwise an error result</returns>
    Task<Result<GoogleReCaptchaResponse>> GetTokenValidationResponseAsync(string token, string secretKey, CancellationToken cancellationToken);
}