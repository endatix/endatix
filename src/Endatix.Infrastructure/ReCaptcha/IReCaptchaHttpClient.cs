using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Endatix.Infrastructure.ReCaptcha;

public interface IReCaptchaHttpClient
{
    Task<HttpResponseMessage> VerifyTokenAsync(HttpContent content, CancellationToken cancellationToken);
} 