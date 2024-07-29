using Microsoft.Extensions.DependencyInjection;

namespace Endatix.Samples.CustomEventHandlers;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddContactUsFormOptions(this IServiceCollection services)
    {
        services.AddOptions<ContactUsOptions>()
                .BindConfiguration(ContactUsOptions.CONFIG_SECTION_KEY)
                .ValidateDataAnnotations();

        return services;
    }

}

