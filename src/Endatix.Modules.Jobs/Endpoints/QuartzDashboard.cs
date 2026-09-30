using Endatix.Modules.Jobs.Runtime;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Quartz;

namespace Endatix.Modules.Jobs.Endpoints;

/// <summary>
/// The scheduler's operator dashboard and its HTTP API, mapped only when
/// <c>Endatix:BackgroundJobs:Dashboard:Enabled</c> is set.
/// </summary>
/// <remarks>
/// Both require the <c>PlatformAdmin</c> policy and are never anonymous. They are read-only unless
/// <c>Dashboard:AllowWrites</c> is set, and even then only Endatix's own job class may be named, because a caller
/// that may write can otherwise schedule any job type on the host. Tenant users see their jobs through
/// <c>GET jobs/{jobId}</c>, never here.
/// </remarks>
internal static class QuartzDashboard
{
    public const string DashboardPath = "/quartz";
    public const string ApiPath = "/quartz-api";
    public const string AuthorizationPolicy = "PlatformAdmin";

    private static readonly string _endatixJobClass = typeof(BackgroundJobExecution).FullName!;

    public static IServiceCollection AddJobsDashboard(this IServiceCollection services, IConfiguration configuration)
    {
        var options = configuration.GetSection(BackgroundJobsOptions.SectionName).Get<BackgroundJobsOptions>()
            ?? new BackgroundJobsOptions();
        if (!options.Dashboard.Enabled)
        {
            return services;
        }

        var readOnly = !options.Dashboard.AllowWrites;
        services.AddQuartzHttpApi(api => ConfigureApi(api, readOnly));
        services.AddQuartzDashboard(dashboard => ConfigureDashboard(dashboard, readOnly));
        services.AddSingleton<IStartupFilter, MapDashboardAfterApplication>();

        return services;
    }

    private static void ConfigureApi(QuartzHttpApiOptions api, bool readOnly)
    {
        api.ApiPath = ApiPath;
        api.ReadOnly = readOnly;
        api.IsJobTypeAllowed = IsEndatixJobClass;
    }

    private static void ConfigureDashboard(QuartzDashboardOptions dashboard, bool readOnly)
    {
        dashboard.DashboardPath = DashboardPath;
        dashboard.ReadOnly = readOnly;
        dashboard.AuthorizationPolicy = AuthorizationPolicy;
        dashboard.IsJobTypeAllowed = IsEndatixJobClass;
    }

    // The job type arrives as the name the caller wrote, with or without its assembly. A bare prefix match would
    // also admit any other type whose name merely starts with this one's.
    internal static bool IsEndatixJobClass(string jobType) =>
        jobType.StartsWith(_endatixJobClass, StringComparison.Ordinal)
        && (jobType.Length == _endatixJobClass.Length || jobType[_endatixJobClass.Length] == ',');

    /// <summary>
    /// Appends the dashboard to the end of the application's pipeline, after the application's own endpoints,
    /// so a request reaches it only when nothing in the application answered.
    /// </summary>
    private sealed class MapDashboardAfterApplication : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            next(app);

            app.UseRouting();
            app.UseAuthorization();
            app.UseAntiforgery();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapQuartzHttpApi(ApiPath).RequireAuthorization(AuthorizationPolicy);
                endpoints.MapQuartzDashboard(DashboardPath).RequireAuthorization(AuthorizationPolicy);
            });
        };
    }
}
