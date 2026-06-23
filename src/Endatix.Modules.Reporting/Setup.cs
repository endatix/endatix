using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Endatix.Framework.Modules;

namespace Endatix.Modules.Reporting;

/// <summary>
/// Dependency injection registration for the Reporting module.
/// </summary>
public static class Setup
{
    [Obsolete("Reporting is registered via EndatixBuilder.UseDefaults(). Use UseModule for custom modules.")]
    public static IServiceCollection AddReportingModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ReportingModule.Instance.ConfigureServices(new EndatixModuleBuilder(services, configuration));
        return services;
    }
}
