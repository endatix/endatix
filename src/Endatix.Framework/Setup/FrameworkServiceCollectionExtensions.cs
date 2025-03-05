using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Endatix.Framework.Hosting;

namespace Endatix.Framework.Setup;

/// <summary>
/// Extension methods for configuring Endatix Framework services.
/// </summary>
public static class FrameworkServiceCollectionExtensions
{
    /// <summary>
    /// Adds the core Endatix Framework services to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for method chaining.</returns>
    public static IServiceCollection AddEndatixFrameworkServices(this IServiceCollection services)
    {
        // Ensure we have an IHostEnvironment
        EnsureHostEnvironment(services);

        // Register IAppEnvironment
        services.AddSingleton<IAppEnvironment>(provider =>
        {
            // Try to resolve IWebHostEnvironment first
            var webEnv = provider.GetService<IWebHostEnvironment>();
            if (webEnv != null)
            {
                return new AppEnvironment(webEnv);
            }

            // Fall back to IHostEnvironment if IWebHostEnvironment is not available
            var hostEnv = provider.GetService<IHostEnvironment>();
            if (hostEnv != null)
            {
                // Adapt the IHostEnvironment to IWebHostEnvironment
                var adapter = new HostingEnvironmentAdapter(hostEnv);
                return new AppEnvironment(adapter);
            }

            // Last resort - create a development environment
            var devEnv = new DevelopmentEnvironment();
            var devAdapter = new HostingEnvironmentAdapter(devEnv);
            return new AppEnvironment(devAdapter);
        });

        return services;
    }

    /// <summary>
    /// Ensures that an IHostEnvironment is registered in the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    private static void EnsureHostEnvironment(IServiceCollection services)
    {
        // If IHostEnvironment is already registered, there's nothing to do
        if (services.Any(sd => sd.ServiceType == typeof(IHostEnvironment)))
        {
            return;
        }

        // Register a default implementation that assumes development environment
        services.AddSingleton<IHostEnvironment>(new DevelopmentEnvironment());
    }

    // Helper classes for environment adaptation
    private class HostingEnvironmentAdapter : IWebHostEnvironment
    {
        private readonly IHostEnvironment _environment;

        public HostingEnvironmentAdapter(IHostEnvironment environment)
        {
            _environment = environment;
        }

        public string WebRootPath { get; set; } = string.Empty;
        public IFileProvider WebRootFileProvider { get; set; } = null!;

        public string EnvironmentName
        {
            get => _environment.EnvironmentName;
            set => _environment.EnvironmentName = value;
        }

        public string ApplicationName
        {
            get => _environment.ApplicationName;
            set => _environment.ApplicationName = value;
        }

        public string ContentRootPath
        {
            get => _environment.ContentRootPath;
            set => _environment.ContentRootPath = value;
        }

        public IFileProvider ContentRootFileProvider
        {
            get => _environment.ContentRootFileProvider;
            set => _environment.ContentRootFileProvider = value;
        }
    }

    private class DevelopmentEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "Endatix.Framework";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}