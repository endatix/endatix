using Microsoft.Extensions.Options;
using Endatix.Samples.WebApp.ApiClient;

namespace Endatix.Samples.WebApp;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddEndatixApiClient(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<HttpClientOptions>()
                .BindConfiguration(HttpClientOptions.CONFIG_SECTION_KEY)
                .ValidateDataAnnotations();

        services.AddHttpClient<IEndatixClient, EndatixClient>((serviceProvider, client) =>
                {
                    var endatixClientSettings = serviceProvider.GetRequiredService<IOptions<HttpClientOptions>>().Value;
                    var baseUrl = endatixClientSettings.ApiBaseUrl;

                    client.BaseAddress = new Uri($"{baseUrl}/api/");
                    client.DefaultRequestHeaders.Add("User-Agent", "Endatix Client");
                });

        return services;
    }

}
