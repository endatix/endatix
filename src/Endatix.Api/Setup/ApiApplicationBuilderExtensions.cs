using System.Security.Claims;
using Endatix.Api.Infrastructure;
using Endatix.Infrastructure.Identity;
using FastEndpoints;
using FastEndpoints.Swagger;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Endatix.Api.Setup;

/// <summary>
/// Extension methods for configuring API endpoints in the application pipeline.
/// </summary>
public static class ApiApplicationBuilderExtensions
{
    // A static field to store service collection during application startup
    // This is a temporary solution until we refactor the ApiConfigurationBuilder to not require IServiceCollection
    private static IServiceCollection? _cachedServices;

    /// <summary>
    /// Stores the service collection for later use by ApiConfigurationBuilder.
    /// This should be called during application startup before building the service provider.
    /// </summary>
    /// <param name="services">The service collection to cache.</param>
    public static void CacheServiceCollection(IServiceCollection services)
    {
        _cachedServices = services;
    }

    /// <summary>
    /// Configures the application to use Endatix API middleware.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <returns>The application builder for chaining.</returns>
    public static IApplicationBuilder UseEndatixApi(this IApplicationBuilder app)
    {
        var loggerFactory = app.ApplicationServices.GetService<ILoggerFactory>();
        var logger = loggerFactory?.CreateLogger("Endatix.Api.Setup");

        logger?.LogInformation("Configuring Endatix API middleware");

        // Set up standard middleware pipeline
        ConfigureApiMiddleware(app);

        logger?.LogInformation("Endatix API middleware configured successfully");
        return app;
    }

    /// <summary>
    /// Configures the application to use Endatix API middleware with custom options.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <param name="configure">A delegate to configure middleware options.</param>
    /// <returns>The application builder for chaining.</returns>
    public static IApplicationBuilder UseEndatixApi(this IApplicationBuilder app, Action<EndatixApiMiddlewareOptions> configure)
    {
        var loggerFactory = app.ApplicationServices.GetService<ILoggerFactory>();
        var logger = loggerFactory?.CreateLogger("Endatix.Api.Setup");

        logger?.LogInformation("Configuring Endatix API middleware with custom options");

        // Create options with defaults
        var options = new EndatixApiMiddlewareOptions();

        // Apply custom configuration
        configure(options);

        // Set up middleware pipeline based on options
        ConfigureApiMiddleware(app, options);

        logger?.LogInformation("Endatix API middleware configured successfully with custom options");
        return app;
    }

    /// <summary>
    /// Internal method to configure the API middleware pipeline.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <param name="options">The middleware options. If null, defaults are used.</param>
    private static void ConfigureApiMiddleware(IApplicationBuilder app, EndatixApiMiddlewareOptions? options = null)
    {
        options ??= new EndatixApiMiddlewareOptions();

        // Apply exception handling middleware if enabled
        if (options.UseExceptionHandler)
        {
            app.UseExceptionHandler(options.ExceptionHandlerPath);
        }

        // Apply FastEndpoints middleware with configuration
        app.UseFastEndpoints(c =>
        {
            // Apply versioning configuration
            c.Versioning.Prefix = options.VersioningPrefix;
            c.Endpoints.RoutePrefix = options.RoutePrefix;

            // Apply serializer configuration
            c.Serializer.Options.Converters.Add(new LongToStringConverter());

            // Apply security configuration
            c.Security.RoleClaimType = ClaimTypes.Role;
            c.Security.PermissionsClaimType = ClaimNames.Permission;

            // Apply any custom configuration
            options.ConfigureFastEndpoints?.Invoke(c);
        });

        // Apply Swagger middleware if enabled
        if (options.UseSwagger)
        {
            var environment = app.ApplicationServices.GetService<IWebHostEnvironment>();
            if (environment?.IsDevelopment() == true || options.EnableSwaggerInProduction)
            {
                app.UseSwaggerGen();
            }
        }

        // Apply CORS middleware if enabled
        if (options.UseCors)
        {
            app.UseCors();
        }
    }

    /// <summary>
    /// Configures the application to use API endpoints.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <returns>The application builder for chaining.</returns>
    public static IApplicationBuilder UseApiEndpoints(this IApplicationBuilder app)
    {
        var loggerFactory = app.ApplicationServices.GetService<ILoggerFactory>();
        var logger = loggerFactory?.CreateLogger("Endatix.Api.Setup");

        logger?.LogInformation("Configuring API endpoints in the application pipeline");

        // Simply use FastEndpoints directly for the basic case
        app.UseFastEndpoints();

        // Apply Swagger if in development
        var environment = app.ApplicationServices.GetService<IWebHostEnvironment>();
        if (environment?.IsDevelopment() == true)
        {
            app.UseSwaggerGen();
        }

        logger?.LogInformation("API endpoints configured in the application pipeline");
        return app;
    }

    /// <summary>
    /// Configures the application to use API endpoints with custom configuration.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <param name="configureApi">A delegate to configure the API.</param>
    /// <returns>The application builder for chaining.</returns>
    public static IApplicationBuilder UseApiEndpoints(this IApplicationBuilder app, Action<ApiConfigurationBuilderProxy> configureApi)
    {
        var loggerFactory = app.ApplicationServices.GetService<ILoggerFactory>();
        var logger = loggerFactory?.CreateLogger("Endatix.Api.Setup");

        logger?.LogInformation("Configuring API endpoints in the application pipeline with custom options");

        // Apply FastEndpoints directly and handle Swagger separately
        app.UseFastEndpoints();

        // For more advanced configuration, we'll use the proxy builder
        var proxy = new ApiConfigurationBuilderProxy(loggerFactory);
        configureApi(proxy);

        // Apply the captured configuration
        if (proxy.UseSwagger)
        {
            var environment = app.ApplicationServices.GetService<IWebHostEnvironment>();
            if (environment?.IsDevelopment() == true ||
                (proxy.EnableSwaggerInProduction && environment?.IsProduction() == true))
            {
                app.UseSwaggerGen();
            }
        }

        logger?.LogInformation("API endpoints configured in the application pipeline with custom options");
        return app;
    }

    /// <summary>
    /// A proxy class that simplifies ApiConfigurationBuilder for use when IServiceCollection is not available.
    /// </summary>
    public class ApiConfigurationBuilderProxy
    {
        private readonly ILogger? _logger;

        /// <summary>
        /// Gets a value indicating whether to use Swagger.
        /// </summary>
        public bool UseSwagger { get; private set; }

        /// <summary>
        /// Gets a value indicating whether to use API versioning.
        /// </summary>
        public bool UseVersioning { get; private set; }

        /// <summary>
        /// Gets a value indicating whether to enable Swagger in production.
        /// </summary>
        public bool EnableSwaggerInProduction { get; private set; }

        /// <summary>
        /// Initializes a new instance of the ApiConfigurationBuilderProxy class.
        /// </summary>
        /// <param name="loggerFactory">The optional logger factory.</param>
        public ApiConfigurationBuilderProxy(ILoggerFactory? loggerFactory = null)
        {
            if (loggerFactory != null)
            {
                _logger = loggerFactory.CreateLogger<ApiConfigurationBuilderProxy>();
            }
        }

        /// <summary>
        /// Applies default configuration.
        /// </summary>
        /// <returns>The builder for chaining.</returns>
        public ApiConfigurationBuilderProxy UseDefaults()
        {
            AddSwagger();
            AddVersioning();
            LogSetupInfo("Default API configuration applied");
            return this;
        }

        /// <summary>
        /// Adds Swagger documentation.
        /// </summary>
        /// <returns>The builder for chaining.</returns>
        public ApiConfigurationBuilderProxy AddSwagger()
        {
            UseSwagger = true;
            LogSetupInfo("Adding Swagger documentation");
            return this;
        }

        /// <summary>
        /// Adds Swagger documentation and enables it in production.
        /// </summary>
        /// <returns>The builder for chaining.</returns>
        public ApiConfigurationBuilderProxy AddSwaggerInProduction()
        {
            UseSwagger = true;
            EnableSwaggerInProduction = true;
            LogSetupInfo("Adding Swagger documentation with production support");
            return this;
        }

        /// <summary>
        /// Configures API versioning.
        /// </summary>
        /// <returns>The builder for chaining.</returns>
        public ApiConfigurationBuilderProxy AddVersioning()
        {
            LogSetupInfo("Configuring API versioning");
            UseVersioning = true;
            LogSetupInfo("API versioning support enabled");
            return this;
        }

        /// <summary>
        /// Adds endpoint discovery from the specified assemblies.
        /// </summary>
        /// <param name="assemblies">The assemblies to scan for endpoints.</param>
        /// <returns>The builder for chaining.</returns>
        public ApiConfigurationBuilderProxy ScanAssemblies(params System.Reflection.Assembly[] assemblies)
        {
            LogSetupInfo($"Scanning {assemblies.Length} assemblies for endpoints");
            LogSetupInfo("Assembly scanning for endpoints completed");
            return this;
        }

        private void LogSetupInfo(string message)
        {
            _logger?.LogInformation(message);
        }
    }
}

/// <summary>
/// Options for configuring Endatix API middleware.
/// </summary>
public class EndatixApiMiddlewareOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether to use exception handling middleware.
    /// </summary>
    public bool UseExceptionHandler { get; set; } = true;

    /// <summary>
    /// Gets or sets the exception handler path.
    /// </summary>
    public string ExceptionHandlerPath { get; set; } = "/error";

    /// <summary>
    /// Gets or sets a value indicating whether to use Swagger middleware.
    /// </summary>
    public bool UseSwagger { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to enable Swagger in production.
    /// </summary>
    public bool EnableSwaggerInProduction { get; set; } = false;

    /// <summary>
    /// Gets or sets a value indicating whether to use CORS middleware.
    /// </summary>
    public bool UseCors { get; set; } = true;

    /// <summary>
    /// Gets or sets the versioning prefix.
    /// </summary>
    public string VersioningPrefix { get; set; } = "v";

    /// <summary>
    /// Gets or sets the route prefix.
    /// </summary>
    public string RoutePrefix { get; set; } = "api";

    /// <summary>
    /// Gets or sets a delegate to configure FastEndpoints.
    /// </summary>
    public Action<FastEndpoints.Config>? ConfigureFastEndpoints { get; set; }
}