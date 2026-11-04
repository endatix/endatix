using Endatix.Core.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.Infrastructure.Features.Submissions;

/// <summary>
/// Registers the host's custom collection status codes, each with its phase, from
/// <c>Endatix:Submissions:CollectionStatuses</c>, for example <c>"panel_overquota": "closed"</c>.
/// </summary>
public static class CollectionStatusRegistration
{
    /// <summary>The configuration section that maps custom codes to phases.</summary>
    public const string SectionPath = "Endatix:Submissions:CollectionStatuses";

    /// <summary>
    /// Reads the host's codes and registers them in <see cref="CollectionStatusCatalog"/>, and adds the startup check
    /// that logs stored codes nobody registered.
    /// </summary>
    /// <exception cref="InvalidOperationException">A code names no phase, or the catalog refuses it.</exception>
    public static IServiceCollection AddCollectionStatuses(this IServiceCollection services, IConfiguration configuration)
    {
        CollectionStatusCatalog.Configure(Read(configuration.GetSection(SectionPath)));
        services.AddHostedService<UnregisteredCollectionStatusesCheck>();
        return services;
    }

    internal static Dictionary<string, CollectionPhase> Read(IConfigurationSection section)
    {
        Dictionary<string, CollectionPhase> codes = new(StringComparer.Ordinal);
        foreach (var entry in section.GetChildren())
        {
            if (!CollectionPhaseNames.TryParse(entry.Value, out var phase))
            {
                throw new InvalidOperationException(
                    $"{SectionPath}:{entry.Key} must be one of pending, active, completed, closed, lapsed; it is '{entry.Value}'.");
            }

            codes[entry.Key] = phase;
        }

        return codes;
    }
}
