using Ardalis.GuardClauses;
using Endatix.Framework.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.FeatureManagement;

namespace Endatix.Framework.FeatureFlags;

/// <summary>
/// Registers Microsoft Feature Management for Data Lists flags and the feature gate service.
/// </summary>
public static class FeatureFlagsServiceCollectionExtensions
{
    /// <summary>
    /// Adds feature management scoped to <c>Endatix:FeatureFlags</c> and <see cref="IFeatureGate"/>.
    /// </summary>
    public static IServiceCollection AddEndatixFeatureFlags(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        Guard.Against.Null(services);
        Guard.Against.Null(configuration);

        var sectionPath = $"{EndatixOptionsBase.RootSectionName}:{FeatureFlagsOptions.RelativeSectionPath}";
        var featureSection = configuration.GetSection(sectionPath);

        services.AddEndatixOptions<FeatureFlagsOptions>(configuration);
        services
            .AddScopedFeatureManagement(featureSection)
            .WithTargeting<FeatureFlagsTargetingContext>();

        services.AddScoped<IFeatureGate, FeatureGate>();
        return services;
    }
}
