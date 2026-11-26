using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Endatix.Core.Abstractions.Authorization;

/// <summary>Configures required-action enforcement for a consuming host.</summary>
[Experimental("ENDATIX0006")]
public static class RequiredActionServiceCollectionExtensions
{
    /// <summary>Checks account standing using the current request's authorization data.</summary>
    public static IServiceCollection AddRequiredActionEnforcement(this IServiceCollection services) =>
        services.Replace(ServiceDescriptor.Scoped(sp =>
            new AccountStanding(sp.GetRequiredService<ICurrentUserAuthorizationService>(), sp.GetService<IDateTimeProvider>())));
}
