using Endatix.Core.Abstractions.Submitters;
using Endatix.Framework.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Endatix.Infrastructure.Features.Submitters;

public static class SubmitterServiceCollectionExtensions
{
    public static IServiceCollection AddSubmitterResolution(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddEndatixOptions<SubmitterOptions>(configuration);
        services.AddSingleton<IValidateOptions<SubmitterOptions>>(new SubmitterOptionsValidator(configuration));
        services.AddOptions<SubmitterOptions>().ValidateOnStart();
        services.AddSingleton<SubmitterClaimReader>();
        services.AddSingleton<SubmitterProfileSnapshotBuilder>();
        services.AddScoped<ISubmitterClaimExtractor, KeycloakSubmitterClaimExtractor>();
        services.AddScoped<ISubmitterClaimExtractor, EndatixSubmitterClaimExtractor>();
        services.AddScoped<ISubmitterClaimExtractor, AnonymousSubmitterClaimExtractor>();
        services.AddScoped<ISubmitterResolver, SubmitterResolver>();

        return services;
    }
}
