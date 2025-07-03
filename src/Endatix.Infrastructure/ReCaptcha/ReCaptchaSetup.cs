using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Endatix.Framework.Configuration;
using Endatix.Core.Features.ReCaptcha;

namespace Endatix.Infrastructure.ReCaptcha;

public static class ReCaptchaSetup
{
    public static IServiceCollection AddReCaptcha(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddEndatixOptions<ReCaptchaOptions>(configuration);
        services.AddHttpClient<IReCaptchaHttpClient, ReCaptchaHttpClient>((sp, client) =>
        {
            client.BaseAddress = new Uri("https://www.google.com/recaptcha/api/");
            client.Timeout = TimeSpan.FromSeconds(5);
        });
        services.AddScoped<IReCaptchaPolicyService, GoogleReCaptchaService>();
        return services;
    }
}