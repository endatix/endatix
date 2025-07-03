using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Endatix.Infrastructure.ReCaptcha;

public class ReCaptchaHttpClient(HttpClient client) : IReCaptchaHttpClient
{
    public async Task<HttpResponseMessage> VerifyTokenAsync(HttpContent content, CancellationToken cancellationToken)
    {
        return await client.PostAsync("https://www.google.com/recaptcha/api/siteverify", content, cancellationToken);
    }
} 