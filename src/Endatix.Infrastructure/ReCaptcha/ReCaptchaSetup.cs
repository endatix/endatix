using Endatix.Core.Features.ReCaptcha;
using Endatix.Framework.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.Infrastructure.ReCaptcha;

public static class ReCaptchaSetup
{
    public static IServiceCollection AddReCaptcha(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddEndatixOptions<ReCaptchaOptions>(configuration);
        services.AddHttpClient<IReCaptchaHttpClient, ReCaptchaHttpClient>((sp, client) =>
        {
#pragma warning disable S1075 // Google's fixed reCAPTCHA API origin.
            client.BaseAddress = new Uri("https://www.google.com/recaptcha/api/");
#pragma warning restore S1075
            client.Timeout = TimeSpan.FromSeconds(5);
        });
        services.AddScoped<IReCaptchaPolicyService, GoogleReCaptchaService>();
        return services;
    }
}
